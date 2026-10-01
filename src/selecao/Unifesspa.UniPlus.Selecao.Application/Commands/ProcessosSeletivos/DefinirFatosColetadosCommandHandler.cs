namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Handler do <see cref="DefinirFatosColetadosCommand"/> (Story #984): substitui integralmente
/// os itens do formulário de uma finalidade, em duas passadas que acumulam no mesmo
/// <c>errors[]</c> (ADR-0125). A primeira confirma a <b>forma</b> de todos os itens — campos
/// básicos e obrigatoriedade — sem tocar o vocabulário cross-módulo. A segunda resolve, para os
/// itens de forma válida, a <b>coletabilidade</b> (só se coleta fato <c>Origem = DECLARADO</c>
/// com binding de campo de inscrição) e a validação <b>semântica</b> das regras (operador ×
/// domínio × valor do fato citado, contra a oferta do próprio processo para os domínios
/// dinâmicos), com o vocabulário cross-módulo (<see cref="IFatoCandidatoReader"/>, ADR-0056).
/// A validação <b>estrutural</b> do grafo (ordem única, regra cita fato coletado e anterior,
/// aciclicidade) e o guard de rascunho são do agregado
/// (<see cref="ProcessoSeletivo.DefinirFatosColetados"/>).
/// </summary>
public static class DefinirFatosColetadosCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirFatosColetadosCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição (e o bloqueio de mutação pós-publicação sem sessão) é conferida AQUI, ANTES
        // da resolução do vocabulário cross-módulo: um processo publicado sem sessão, ou um cliente
        // com If-Match defasado, é recusado sem pagar o I/O do reader nem caçar um fato que ele não
        // errou. O mesmo guard continua dentro de DefinirFatosColetados.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        // Forma, leitura do catálogo e semântica acumulam no mesmo errors[] (ADR-0125), e a decisão
        // é tomada num ponto só. A forma vem antes da leitura, sem I/O; o item de forma inválida só
        // não segue para a conferência contra o catálogo.
        List<FieldError> erros = [];
        RegrasDoItem?[] regras = new RegrasDoItem?[command.Itens.Count];
        for (int indice = 0; indice < command.Itens.Count; indice++)
        {
            FatoColetadoInput input = command.Itens[indice];
            string campo = $"itens[{indice}]";
            int recusasAntes = erros.Count;
            erros.AddRange(FormaDoItem.ValidarFormaBasica(
                    input.FatoCodigo, input.Ordem, input.Rotulo, TipoRenderizacaoCodigo.FromCodigo(input.TipoRenderizacao))
                .Select(erro => erro with { Field = $"{campo}.{erro.Field}" }));

            Obrigatoriedade? obrigatoriedade = ConferirObrigatoriedade(input, campo, erros);
            IReadOnlyList<RestricaoValor> restricoes = ConferirRestricoes(input, campo, erros);
            regras[indice] = erros.Count == recusasAntes ? new RegrasDoItem(obrigatoriedade!, restricoes) : null;
        }

        IReadOnlyList<FatoCandidatoView> fatosDoCatalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, FatoCandidatoView> catalogo = fatosDoCatalogo.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        Dictionary<string, DescritorFatoCandidato> vocabulario = VocabularioDeFatos.Descritores(fatosDoCatalogo);

        // O domínio dos fatos categóricos cuja fonte é o processo vem do PRÓPRIO processo — uma
        // regra que os cite valida contra ele, nunca contra um catálogo global.
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos =
            VocabularioDeFatos.DominiosDinamicos(processo, fatosDoCatalogo);

        List<FatoColetado> fatos = [];
        for (int indice = 0; indice < command.Itens.Count; indice++)
        {
            if (regras[indice] is not { } regrasDoItem)
            {
                continue;
            }

            string campo = $"itens[{indice}]";
            Result<FatoColetado> fato = ResolverFato(command.Itens[indice], regrasDoItem, catalogo, vocabulario, dominiosDinamicos);
            if (fato.IsSuccess)
            {
                fatos.Add(fato.Value!);
            }
            else
            {
                erros.AddRange(fato.Errors.Select(erro => erro with { Field = string.IsNullOrEmpty(erro.Field) ? campo : $"{campo}.{erro.Field}" }));
            }
        }

        if (erros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(erros);
        }

        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            catalogo,
            processo.Vinculos(),
            VinculosDeFatos.De(
                fatos.Select(static f => f.FatoCodigo),
                fatos.SelectMany(static f => f.Condicoes).Select(static c => (c.Fato, c.Valor))));
        if (vinculoNovo.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(vinculoNovo.Error!);
        }

        Result result = processo.DefinirFatosColetados(command.Finalidade, fatos, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(result.Errors);
        }

        // Agregado tracked: a substituição da coleção (Clear + filhos novos com Guid v7) é
        // persistida por change detection no SaveChanges — não chamar DbSet.Update.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // Em rascunho puro não há sessão: ETagDaSessaoEditorial é nulo e a resposta é 204 sem ETag.
        // Sob sessão de retificação, a revisão avançou e o ETag novo é devolvido.
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    /// <summary>
    /// A obrigatoriedade do item na forma do termo: <c>SEMPRE</c> ou <c>NUNCA</c> sem predicado,
    /// <c>QUANDO</c> com ele. A semântica do predicado é conferida contra o catálogo, depois.
    /// </summary>
    private static Obrigatoriedade? ConferirObrigatoriedade(FatoColetadoInput input, string campo, List<FieldError> erros)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(input.PredicadoObrigatoriedade);
        if (predicado.IsFailure)
        {
            erros.Add(new($"{campo}.predicadoObrigatoriedade", predicado.Error!));
            return null;
        }

        if (EntradaDeRegras.Obrigatoriedade(input.Obrigatoriedade, predicado.Value) is { } obrigatoriedade)
        {
            return obrigatoriedade;
        }

        erros.Add(new($"{campo}.obrigatoriedade", new DomainError(
            FatoColetadoErrorCodes.ObrigatoriedadeInvalida,
            "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.")));
        return null;
    }

    /// <summary>A forma das restrições de valor do item; os valores e fatos citados são conferidos contra o catálogo, depois.</summary>
    private static List<RestricaoValor> ConferirRestricoes(FatoColetadoInput input, string campo, List<FieldError> erros)
    {
        List<RestricaoValor> restricoes = [];
        IReadOnlyList<RestricaoValorInput> entradas = input.Restricoes ?? [];
        for (int indice = 0; indice < entradas.Count; indice++)
        {
            Result<RestricaoValor> restricao = entradas[indice] is { } entrada
                ? EntradaDeRegras.Restricao(entrada)
                : Result<RestricaoValor>.Failure(new DomainError(RestricaoValorErrorCodes.TipoDesconhecido, "A restrição de valor é nula."));
            if (restricao.IsSuccess)
            {
                restricoes.Add(restricao.Value!);
            }
            else
            {
                erros.Add(new($"{campo}.restricoes[{indice}]", restricao.Error!));
            }
        }

        return restricoes;
    }

    private static Result<FatoColetado> ResolverFato(
        FatoColetadoInput input,
        RegrasDoItem regras,
        Dictionary<string, FatoCandidatoView> catalogo,
        Dictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        // Coletabilidade: o fato existe no vocabulário e é DECLARADO com binding de campo de
        // inscrição. Um derivado (MODALIDADE, binding REGRA_DERIVACAO) ou um computado
        // (RENDA_PER_CAPITA, binding ATRIBUTO_CANDIDATO) não é coletável — o candidato não o
        // responde num campo.
        if (!catalogo.TryGetValue(input.FatoCodigo, out FatoCandidatoView? view))
        {
            return Result<FatoColetado>.ValidationFailure([new("fatoCodigo", new DomainError(
                ColetabilidadeDeFato.FatoDesconhecido,
                $"O fato '{input.FatoCodigo}' não pertence ao vocabulário de fatos do candidato."))]);
        }

        if (!ColetabilidadeDeFato.EhColetavel(view))
        {
            return Result<FatoColetado>.ValidationFailure([new("fatoCodigo", new DomainError(
                ColetabilidadeDeFato.FatoNaoColetavel,
                $"O fato '{input.FatoCodigo}' não é coletável — só um fato declarado, respondido em campo de "
                + "inscrição, pode ser coletado (derivados e computados não)."))]);
        }

        TipoRenderizacao tipoRenderizacao = TipoRenderizacaoCodigo.FromCodigo(input.TipoRenderizacao);
        if (CoerenciaDeRenderizacao.Validar(tipoRenderizacao, view) is { } incoerencia)
        {
            return Result<FatoColetado>.ValidationFailure([new("tipoRenderizacao", incoerencia)]);
        }

        List<FieldError> erros = [];
        Result<IReadOnlyList<CondicaoPrecondicaoFato>?> precondicoesResult =
            ResolverPrecondicao(input.Precondicao, vocabulario, dominiosDinamicos);
        if (precondicoesResult.IsFailure)
        {
            erros.Add(new("precondicao", precondicoesResult.Error!));
        }

        if (regras.Obrigatoriedade.Predicado is { } predicado
            && PredicadoDnfValidador.Validar(predicado, vocabulario, null, dominiosDinamicos) is { IsFailure: true } semantica)
        {
            erros.Add(new("predicadoObrigatoriedade", semantica.Error!));
        }

        erros.AddRange(SemanticaDasRestricoes(view, tipoRenderizacao, regras.Restricoes, catalogo, vocabulario, dominiosDinamicos));

        IEnumerable<string> citados = (input.Precondicao ?? []).SelectMany(static c => c).Where(static c => c is not null).Select(static c => c.Fato)
            .Concat(regras.Obrigatoriedade.FatosCitados)
            .Concat(regras.Restricoes.SelectMany(static r => r.FatosCitados));
        if (VocabularioDeFatos.CitacaoDeAtributoDoCandidato(citados, catalogo) is { } atributo)
        {
            erros.Add(new(string.Empty, atributo));
        }

        // As recusas do próprio item (coerência das restrições com o campo, autorreferência, ajuda)
        // saem junto das semânticas, no mesmo lote.
        Result<FatoColetado> fato = FatoColetado.Criar(
            input.FatoCodigo, input.Ordem, input.Rotulo, tipoRenderizacao, regras.Obrigatoriedade,
            precondicoesResult.IsSuccess ? precondicoesResult.Value : null,
            origemValores: VocabularioDeFatos.OrigemValores(view), etapaCodigo: input.EtapaCodigo, formato: view.Formato,
            ajuda: input.Ajuda, pedirConfirmacao: input.PedirConfirmacao, restricoes: regras.Restricoes);
        return erros.Count == 0
            ? fato
            : Result<FatoColetado>.ValidationFailure([.. erros, .. fato.IsFailure ? fato.Errors : []]);
    }

    /// <summary>
    /// As restrições contra o catálogo: a condição de cada grupo de opções valida como qualquer
    /// predicado; os valores permitidos são do domínio do próprio fato, conferidos como a condição
    /// <c>FATO EM [valores]</c>; e as respostas que formam as opções vêm de campos categóricos cujas
    /// opções são todas opções do campo, para que toda resposta anterior seja uma opção válida.
    /// A restrição que não cabe no tipo do campo fica para a recusa de coerência do item.
    /// </summary>
    private static IEnumerable<FieldError> SemanticaDasRestricoes(
        FatoCandidatoView alvo,
        TipoRenderizacao tipoRenderizacao,
        IReadOnlyList<RestricaoValor> restricoes,
        Dictionary<string, FatoCandidatoView> catalogo,
        Dictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        for (int indice = 0; indice < restricoes.Count; indice++)
        {
            if (!FormaDoItem.RestricaoCabeNoCampo(restricoes[indice].Tipo, tipoRenderizacao))
            {
                continue;
            }

            string campo = $"restricoes[{indice}]";
            switch (restricoes[indice])
            {
                case OpcoesPermitidas opcoes:
                    for (int entrada = 0; entrada < opcoes.Entradas.Count; entrada++)
                    {
                        OpcoesCondicionadas grupo = opcoes.Entradas[entrada];
                        if (grupo.Quando is { } quando
                            && PredicadoDnfValidador.Validar(quando, vocabulario, null, dominiosDinamicos) is { IsFailure: true } condicao)
                        {
                            yield return new($"{campo}.entradas[{entrada}].quando", condicao.Error!);
                        }

                        PredicadoDnf pertinencia = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(
                            alvo.Codigo, Operador.Em, JsonSerializer.SerializeToElement(grupo.Valores.Order(StringComparer.Ordinal))).Value!)]).Value!;
                        if (PredicadoDnfValidador.Validar(pertinencia, vocabulario, null, dominiosDinamicos) is { IsFailure: true } valores)
                        {
                            yield return new($"{campo}.entradas[{entrada}].valores", valores.Error!);
                        }
                    }

                    break;
                case OpcoesDasRespostas respostas
                    when respostas.Fatos.Any(f => !catalogo.TryGetValue(f, out FatoCandidatoView? fonte) || !OpcoesDaFonteCabemNoAlvo(alvo, fonte, dominiosDinamicos)):
                    yield return new($"{campo}.fatos", new DomainError(
                        FatoColetadoErrorCodes.OpcoesDeOutroDominio,
                        "As opções formadas pelas respostas vêm de campos de seleção cujas opções são todas opções do campo."));
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Toda resposta possível da fonte é opção do alvo: os valores da fonte cabem nos do alvo; no
    /// domínio por formato, que não enumera valores, a fonte dos valores é a mesma.
    /// </summary>
    private static bool OpcoesDaFonteCabemNoAlvo(
        FatoCandidatoView alvo, FatoCandidatoView fonte, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        string.Equals(fonte.Dominio, alvo.Dominio, StringComparison.Ordinal)
        && (ValoresDoDominio(fonte, dominiosDinamicos), ValoresDoDominio(alvo, dominiosDinamicos)) switch
        {
            (null, null) => string.Equals(fonte.FonteValores, alvo.FonteValores, StringComparison.Ordinal),
            ({ } daFonte, { } doAlvo) => daFonte.IsSubsetOf(doAlvo),
            _ => false,
        };

    /// <summary>Os valores enumerados do domínio do fato — do catálogo ou do processo —; nulo no domínio por formato.</summary>
    private static IReadOnlySet<string>? ValoresDoDominio(FatoCandidatoView fato, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        fato.ValoresDominio is { Count: > 0 } estaticos
            ? estaticos.ToHashSet(StringComparer.Ordinal)
            : dominiosDinamicos.TryGetValue(fato.Codigo, out DominioDeValores? dinamico) ? dinamico.Valores : null;

    /// <summary>A obrigatoriedade e as restrições de um item cuja forma foi aceita.</summary>
    private sealed record RegrasDoItem(Obrigatoriedade Obrigatoriedade, IReadOnlyList<RestricaoValor> Restricoes);

    /// <summary>
    /// Monta e valida a pré-condição de um fato. Primeiro a <b>forma</b> de cada condição
    /// (<see cref="CondicaoDnf.Criar"/>), depois a <b>semântica</b> do predicado inteiro
    /// (<see cref="PredicadoDnfValidador"/>) — fato citado no vocabulário, operador × domínio,
    /// valor × domínio (estático ou dinâmico). A estrutura do grafo (o fato citado é coletado e
    /// anterior) é do agregado, em <see cref="ProcessoSeletivo.DefinirFatosColetados"/>.
    /// </summary>
    private static Result<IReadOnlyList<CondicaoPrecondicaoFato>?> ResolverPrecondicao(
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? precondicao,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        if (precondicao is null)
        {
            return Result<IReadOnlyList<CondicaoPrecondicaoFato>?>.Success(null);
        }

        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        List<CondicaoPrecondicaoFato> condicoes = [];
        for (int clausula = 0; clausula < precondicao.Count; clausula++)
        {
            foreach (CondicaoPrecondicaoInput condicaoInput in precondicao[clausula])
            {
                // Defesa em profundidade: uma condição nula (JSON `[[null]]`) já é barrada pelo
                // validator (400), mas nunca deve virar NullReferenceException/500 aqui.
                if (condicaoInput is null)
                {
                    return Result<IReadOnlyList<CondicaoPrecondicaoFato>?>.Failure(new DomainError(
                        CondicaoPrecondicaoFatoErrorCodes.ClausulaInvalida,
                        "A pré-condição contém uma condição nula."));
                }

                Operador operador = OperadorCodigo.FromCodigo(condicaoInput.Operador);

                Result<CondicaoPrecondicaoFato> condicaoResult =
                    CondicaoPrecondicaoFato.Criar(clausula, condicaoInput.Fato, operador, condicaoInput.Valor);
                if (condicaoResult.IsFailure)
                {
                    return Result<IReadOnlyList<CondicaoPrecondicaoFato>?>.Failure(condicaoResult.Error!);
                }

                condicoes.Add(condicaoResult.Value!);
                linhas.Add((clausula, CondicaoDnf.Criar(condicaoInput.Fato, operador, condicaoInput.Valor).Value!));
            }
        }

        Result<PredicadoDnf> predicadoResult = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
        if (predicadoResult.IsFailure)
        {
            return Result<IReadOnlyList<CondicaoPrecondicaoFato>?>.Failure(predicadoResult.Error!);
        }

        Result validacao = PredicadoDnfValidador.Validar(predicadoResult.Value!, vocabulario, null, dominiosDinamicos);
        return validacao.IsFailure
            ? Result<IReadOnlyList<CondicaoPrecondicaoFato>?>.Failure(validacao.Error!)
            : Result<IReadOnlyList<CondicaoPrecondicaoFato>?>.Success(condicoes);
    }

}

/// <summary>
/// Política de coletabilidade de um fato do candidato (Story #984). Só é coletável — respondido
/// pelo candidato num campo do formulário de inscrição — o fato de <c>Origem = DECLARADO</c>
/// cujo binding aponta para um campo de inscrição (<c>CAMPO_INSCRICAO:{campo}</c>). Um fato
/// derivado (<c>REGRA_DERIVACAO:…</c>) ou computado de atributo (<c>ATRIBUTO_CANDIDATO:…</c>)
/// não é respondido diretamente e não pode ser coletado. Enquanto <c>CAMPO_INSCRICAO</c> for o
/// único binding coletável, este é o critério; um novo binding coletável estende esta política,
/// não os call sites.
/// </summary>
internal static class ColetabilidadeDeFato
{
    public const string FatoDesconhecido = "FatoColetado.FatoDesconhecido";
    public const string FatoNaoColetavel = "FatoColetado.FatoNaoColetavel";

    private const string OrigemDeclarado = "DECLARADO";
    private const string PrefixoBindingCampoInscricao = "CAMPO_INSCRICAO:";

    public static bool EhColetavel(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        return string.Equals(fato.Origem, OrigemDeclarado, StringComparison.Ordinal)
            && fato.Binding.StartsWith(PrefixoBindingCampoInscricao, StringComparison.Ordinal)
            && fato.Binding.Length > PrefixoBindingCampoInscricao.Length;
    }
}

/// <summary>
/// Coerência entre <see cref="TipoRenderizacao"/> e o <c>Dominio</c>/<c>Cardinalidade</c> do
/// fato no catálogo (Story #559) — semântica cross-módulo resolvida na Application, mesmo
/// motivo de <see cref="ColetabilidadeDeFato"/> viver aqui e não no Domain.
/// </summary>
internal static class CoerenciaDeRenderizacao
{
    public const string TipoRenderizacaoIncoerenteComDominio = "FatoColetado.TipoRenderizacaoIncoerenteComDominio";

    private const string DominioBooleano = "BOOLEANO";
    private const string DominioNumerico = "NUMERICO";
    private const string DominioCategorico = "CATEGORICO";
    private const string DominioTexto = "TEXTO";
    private const string CardinalidadeMultivalorado = "MULTIVALORADO";

    public static DomainError? Validar(TipoRenderizacao tipoRenderizacao, FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        bool coerente = fato.Dominio switch
        {
            DominioBooleano => tipoRenderizacao == TipoRenderizacao.Booleano,
            DominioNumerico => tipoRenderizacao == TipoRenderizacao.Numero,
            // O campo de texto recolhe uma resposta só: não há renderização de várias respostas de texto.
            DominioTexto => tipoRenderizacao == TipoRenderizacao.Texto
                && !string.Equals(fato.Cardinalidade, CardinalidadeMultivalorado, StringComparison.Ordinal),
            DominioCategorico => tipoRenderizacao == (string.Equals(fato.Cardinalidade, CardinalidadeMultivalorado, StringComparison.Ordinal)
                ? TipoRenderizacao.SelecaoMultipla
                : TipoRenderizacao.SelecaoUnica),
            _ => false,
        };

        return coerente
            ? null
            : new DomainError(
                TipoRenderizacaoIncoerenteComDominio,
                $"O tipo de renderização '{tipoRenderizacao}' não é coerente com o domínio "
                + $"'{fato.Dominio}'/cardinalidade '{fato.Cardinalidade}' do fato '{fato.Codigo}'.");
    }
}
