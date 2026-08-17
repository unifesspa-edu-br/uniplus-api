namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirFatosColetadosCommand"/> (Story #984): substitui integralmente
/// a coleta de fatos de um processo em rascunho, em duas passadas. A primeira confirma a
/// <b>forma básica</b> de TODOS os fatos (<see cref="FatoColetado.ValidarFormaBasica"/>) sem
/// tocar o vocabulário cross-módulo, acumulando (ADR-0125) através da lista inteira. Só então a
/// segunda resolve <b>coletabilidade</b> (só se coleta fato <c>Origem = DECLARADO</c> com
/// binding de campo de inscrição) e a validação <b>semântica</b> das pré-condições (operador ×
/// domínio × valor do fato citado, contra a oferta do próprio processo para os domínios
/// dinâmicos), que tem acesso ao vocabulário cross-módulo (<see cref="IFatoCandidatoReader"/>,
/// ADR-0056) — parando no primeiro fato que falhar, granularidade nunca coberta pelo
/// FluentValidation. A validação <b>estrutural</b> do grafo (ordem única, pré-condição cita
/// fato coletado e anterior, aciclicidade) e o guard de rascunho são do agregado
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

        // Acumula (ADR-0125) a forma básica de TODOS os fatos (fatoCodigo/ordem/rotulo/
        // tipoRenderizacao) numa primeira passada, ANTES de resolver o vocabulário cross-módulo
        // — o FluentValidation removido já garantia essa granularidade via RuleForEach, rodando
        // como middleware antes do handler. Rodar depois da resolução semântica (achado de
        // revisão) trocaria um fatoCodigo vazio pelo FatoDesconhecido menos específico da busca
        // no catálogo, e uma violação de forma de um fato podia ser mascarada pelo erro
        // semântico de outro fato da mesma lista.
        List<FieldError> formaErros = [];
        for (int indice = 0; indice < command.Fatos.Count; indice++)
        {
            FatoColetadoInput input = command.Fatos[indice];
            List<FieldError> itemErros = FatoColetado.ValidarFormaBasica(
                input.FatoCodigo, input.Ordem, input.Rotulo, TipoRenderizacaoCodigo.FromCodigo(input.TipoRenderizacao));
            formaErros.AddRange(itemErros.Select(erro => erro with { Field = $"fatos[{indice}].{erro.Field}" }));
        }

        if (formaErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(formaErros);
        }

        (IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
            IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario) =
            await ResolverVocabularioAsync(fatoCandidatoReader, cancellationToken).ConfigureAwait(false);

        // O domínio dos fatos categóricos de escopo-processo (CONDICAO_ATENDIMENTO, …) vem da
        // oferta do PRÓPRIO processo — uma pré-condição que os cite valida contra ela, nunca
        // contra um catálogo global.
        IReadOnlyDictionary<string, IReadOnlySet<string>> dominiosDinamicos = ResolverDominiosDinamicos(processo);

        // Segunda passada: forma básica já confirmada para todos os fatos — resolve o
        // vocabulário e a semântica de cada um, parando no primeiro que falhar. Essas checagens
        // (catálogo, coerência de renderização, semântica de pré-condição, autorreferência)
        // nunca foram cobertas pelo FluentValidation e continuam com essa granularidade, como já
        // acontecia antes desta migração.
        List<FatoColetado> fatos = [];
        foreach (FatoColetadoInput input in command.Fatos)
        {
            Result<FatoColetado> fatoResult = ResolverFato(input, catalogo, vocabulario, dominiosDinamicos);
            if (fatoResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(fatoResult.Error!);
            }

            fatos.Add(fatoResult.Value!);
        }

        Result result = processo.DefinirFatosColetados(fatos, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        // Agregado tracked: a substituição da coleção (Clear + filhos novos com Guid v7) é
        // persistida por change detection no SaveChanges — não chamar DbSet.Update.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // Em rascunho puro não há sessão: ETagDaSessaoEditorial é nulo e a resposta é 204 sem ETag.
        // Sob sessão de retificação, a revisão avançou e o ETag novo é devolvido.
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    private static Result<FatoColetado> ResolverFato(
        FatoColetadoInput input,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dominiosDinamicos)
    {
        // Coletabilidade: o fato existe no vocabulário e é DECLARADO com binding de campo de
        // inscrição. Um derivado (MODALIDADE, binding REGRA_DERIVACAO) ou um computado
        // (RENDA_PER_CAPITA, binding ATRIBUTO_CANDIDATO) não é coletável — o candidato não o
        // responde num campo.
        if (!catalogo.TryGetValue(input.FatoCodigo, out FatoCandidatoView? view))
        {
            return Result<FatoColetado>.Failure(new DomainError(
                ColetabilidadeDeFato.FatoDesconhecido,
                $"O fato '{input.FatoCodigo}' não pertence ao vocabulário de fatos do candidato."));
        }

        if (!ColetabilidadeDeFato.EhColetavel(view))
        {
            return Result<FatoColetado>.Failure(new DomainError(
                ColetabilidadeDeFato.FatoNaoColetavel,
                $"O fato '{input.FatoCodigo}' não é coletável — só um fato declarado, respondido em campo de "
                + "inscrição, pode ser coletado (derivados e computados não)."));
        }

        TipoRenderizacao tipoRenderizacao = TipoRenderizacaoCodigo.FromCodigo(input.TipoRenderizacao);
        if (CoerenciaDeRenderizacao.Validar(tipoRenderizacao, view) is { } incoerencia)
        {
            return Result<FatoColetado>.Failure(incoerencia);
        }

        Result<IReadOnlyList<CondicaoPrecondicaoFato>?> precondicoesResult =
            ResolverPrecondicao(input.Precondicao, vocabulario, dominiosDinamicos);
        if (precondicoesResult.IsFailure)
        {
            return Result<FatoColetado>.Failure(precondicoesResult.Error!);
        }

        return FatoColetado.Criar(
            input.FatoCodigo, input.Ordem, input.Rotulo, tipoRenderizacao, input.Obrigatorio, precondicoesResult.Value);
    }

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
        IReadOnlyDictionary<string, IReadOnlySet<string>> dominiosDinamicos)
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

    /// <summary>
    /// Resolve o vocabulário fechado de fatos do candidato numa única passada do reader
    /// cross-módulo: o catálogo cru por código (para a coletabilidade) e a projeção
    /// <see cref="DescritorFatoCandidato"/> (para o validador de predicado), incluindo os
    /// categóricos de escopo-processo como <see cref="TipoDominioFato.CategoricoDinamico"/>.
    /// </summary>
    private static async Task<(
        IReadOnlyDictionary<string, FatoCandidatoView> Catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario)> ResolverVocabularioAsync(
        IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal);
        Dictionary<string, DescritorFatoCandidato> vocabulario = new(StringComparer.Ordinal);
        foreach (FatoCandidatoView fato in fatos)
        {
            catalogo[fato.Codigo] = fato;

            TipoDominioFato? tipoDominio = fato switch
            {
                { Dominio: "BOOLEANO" } => TipoDominioFato.Booleano,
                { Dominio: "NUMERICO" } => TipoDominioFato.Numerico,
                { Dominio: "CATEGORICO", ValoresDominio.Count: > 0 } => TipoDominioFato.CategoricoEstatico,
                { Dominio: "CATEGORICO", ValoresDominio: null } => TipoDominioFato.CategoricoDinamico,
                _ => null,
            };

            if (tipoDominio is not { } tipo)
            {
                continue;
            }

            Result<DescritorFatoCandidato> descritorResult = DescritorFatoCandidato.Criar(fato.Codigo, tipo, fato.ValoresDominio);
            if (descritorResult.IsSuccess)
            {
                vocabulario[fato.Codigo] = descritorResult.Value!;
            }
        }

        return (catalogo, vocabulario);
    }

    /// <summary>
    /// Domínio dinâmico dos fatos categóricos de escopo-processo (CONDICAO_ATENDIMENTO,
    /// TIPO_DEFICIENCIA): o conjunto de valores que o PRÓPRIO processo oferece, para que uma
    /// pré-condição que os cite seja validada contra a oferta e não contra um catálogo global.
    /// </summary>
    private static Dictionary<string, IReadOnlySet<string>> ResolverDominiosDinamicos(ProcessoSeletivo processo)
    {
        HashSet<string> condicoesAtendimento = [.. (processo.OfertaAtendimento?.Condicoes ?? [])
            .Select(static c => c.CondicaoCodigo)];

        HashSet<string> tiposDeficiencia = [.. (processo.OfertaAtendimento?.TiposDeficiencia ?? [])
            .Select(static t => t.TipoDeficienciaNome)];

        return new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["CONDICAO_ATENDIMENTO"] = condicoesAtendimento,
            ["TIPO_DEFICIENCIA"] = tiposDeficiencia,
        };
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
    private const string CardinalidadeMultivalorado = "MULTIVALORADO";

    public static DomainError? Validar(TipoRenderizacao tipoRenderizacao, FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        bool coerente = fato.Dominio switch
        {
            DominioBooleano => tipoRenderizacao == TipoRenderizacao.Booleano,
            DominioNumerico => tipoRenderizacao == TipoRenderizacao.Numero,
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
