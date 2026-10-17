namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Os itens lidos pela forma, sem leitura externa: as regras de cada item cuja forma foi aceita.</summary>
internal sealed record ItensLidos(IReadOnlyList<FatoColetadoInput> Entradas, IReadOnlyList<RegrasDoItem?> Regras, IReadOnlyList<FieldError> Erros);

/// <summary>A obrigatoriedade, as restrições e o impedimento de um item cuja forma foi aceita.</summary>
internal sealed record RegrasDoItem(Obrigatoriedade Obrigatoriedade, IReadOnlyList<RestricaoValor> Restricoes, Impedimento? Impedimento = null);

/// <summary>
/// Os grupos lidos pela forma, sem leitura externa: a exibição e a obrigatoriedade de cada grupo
/// cuja forma foi aceita, e os campos de cada um, lidos como os itens.
/// </summary>
internal sealed record GruposLidos(
    IReadOnlyList<GrupoColetadoInput> Entradas,
    IReadOnlyList<RegrasDoGrupo?> Regras,
    IReadOnlyList<bool> FormaValida,
    IReadOnlyList<ItensLidos> Campos,
    IReadOnlyList<FieldError> Erros);

/// <summary>A exibição e a obrigatoriedade de um grupo cuja forma foi aceita.</summary>
internal sealed record RegrasDoGrupo(PredicadoDnf? Exibicao, Obrigatoriedade Obrigatoriedade);

/// <summary>
/// A escrita dos itens do formulário do processo, sem ler nem mudar o processo: primeiro a forma
/// de cada item, sem I/O; depois, para os itens de forma válida, a coletabilidade (só se coleta fato
/// declarado com vínculo de campo) e a semântica das regras (operador × domínio × valor do fato
/// citado, contra a oferta do próprio processo para os domínios dinâmicos). A validação estrutural
/// do grafo e o guard de mutação são do agregado.
/// </summary>
internal static class EscritaDosItens
{
    public static ItensLidos Ler(IReadOnlyList<FatoColetadoInput> entradas, string caminho = "itens")
    {
        ArgumentNullException.ThrowIfNull(entradas);
        List<FieldError> erros = [];
        RegrasDoItem?[] regras = new RegrasDoItem?[entradas.Count];
        for (int indice = 0; indice < entradas.Count; indice++)
        {
            FatoColetadoInput input = entradas[indice];
            string campo = $"{caminho}[{indice}]";
            int recusasAntes = erros.Count;
            erros.AddRange(FormaDoItem.ValidarFormaBasica(
                    input.FatoCodigo, input.Ordem, input.Rotulo, TipoRenderizacaoCodigo.FromCodigo(input.TipoRenderizacao))
                .Select(erro => erro with { Field = $"{campo}.{erro.Field}" }));

            Obrigatoriedade? obrigatoriedade = ConferirObrigatoriedade(input.Obrigatoriedade, input.PredicadoObrigatoriedade, campo, erros);
            IReadOnlyList<RestricaoValor> restricoes = ConferirRestricoes(input, campo, erros);
            Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(input.Impedimento);
            if (impedimento.IsFailure)
            {
                erros.Add(new($"{campo}.impedimento.quando", impedimento.Error!));
            }

            regras[indice] = erros.Count == recusasAntes ? new RegrasDoItem(obrigatoriedade!, restricoes, impedimento.Value) : null;
        }

        return new ItensLidos(entradas, regras, erros);
    }

    /// <summary>
    /// Os grupos pela forma, sem leitura externa: a exibição e a obrigatoriedade de cada um, a forma
    /// do grupo — código, ordem, rótulo, contagem, quantidade de campos, autorreferência — e os
    /// campos, lidos como os itens no caminho do grupo, sem seção própria.
    /// </summary>
    public static GruposLidos LerGrupos(IReadOnlyList<GrupoColetadoInput> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        List<FieldError> erros = [];
        RegrasDoGrupo?[] regras = new RegrasDoGrupo?[entradas.Count];
        bool[] formaValida = new bool[entradas.Count];
        ItensLidos[] campos = new ItensLidos[entradas.Count];
        for (int indice = 0; indice < entradas.Count; indice++)
        {
            GrupoColetadoInput input = entradas[indice];
            IReadOnlyList<FatoColetadoInput> subitens = input.Subitens ?? [];
            string caminho = $"grupos[{indice}]";
            int recusasAntes = erros.Count;
            Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(input.Exibicao);
            if (exibicao.IsFailure)
            {
                erros.Add(new($"{caminho}.exibicao", exibicao.Error!));
            }

            Obrigatoriedade? obrigatoriedade = ConferirObrigatoriedade(input.Obrigatoriedade, input.PredicadoObrigatoriedade, caminho, erros);
            regras[indice] = erros.Count == recusasAntes ? new RegrasDoGrupo(exibicao.Value, obrigatoriedade!) : null;

            erros.AddRange(FormaDoGrupo.Conferir(
                    input.Codigo, input.Ordem, input.Rotulo, input.Minimo, input.Maximo,
                    [.. subitens.Select(static s => (s?.FatoCodigo, s?.EtapaCodigo, s?.Impedimento is not null))],
                    exibicao.IsSuccess ? exibicao.Value?.FatosCitados ?? [] : [],
                    obrigatoriedade ?? Obrigatoriedade.Nunca)
                .Select(erro => erro with { Field = $"{caminho}.{erro.Field}" }));

            // A identificação da ocorrência do candidato depende só do mínimo e da forma lida dos
            // campos, e sai no mesmo lote das demais recusas, mesmo com campo recusado (ADR-0125).
            campos[indice] = Ler(subitens, $"{caminho}.subitens");
            if (input.IncluiCandidato)
            {
                erros.AddRange(ConferirCandidatoComoMembro(input.Minimo, subitens, campos[indice])
                    .Select(erro => erro with { Field = $"{caminho}.{erro.Field}" }));
            }

            formaValida[indice] = erros.Count == recusasAntes;
            erros.AddRange(campos[indice].Erros);
        }

        return new GruposLidos(entradas, regras, formaValida, campos, erros);
    }

    /// <summary>
    /// O candidato como membro pela forma lida dos campos. O campo de forma recusada já tem a
    /// própria recusa e conta aqui como conforme, para que só a falta do parentesco seja acusada.
    /// </summary>
    private static List<FieldError> ConferirCandidatoComoMembro(int minimo, IReadOnlyList<FatoColetadoInput> subitens, ItensLidos lidos) =>
        CandidatoComoMembro.Conferir(minimo, [.. subitens.Select((subitem, indice) => lidos.Regras[indice] is { } regras
            ? (subitem.FatoCodigo, subitem.Precondicao is { Count: > 0 }, regras.Obrigatoriedade, regras.Restricoes)
            : (subitem.FatoCodigo, false, Obrigatoriedade.Sempre, (IReadOnlyList<RestricaoValor>)[]))]);

    /// <summary>
    /// Os grupos conferidos contra o catálogo, acumulando tudo no mesmo lote (ADR-0125): os campos
    /// como os itens, mas coletando fato de membro; a semântica da exibição e da obrigatoriedade
    /// do grupo e a recusa de fato calculado de atributo, sempre que as regras tiverem forma. O
    /// grupo só é montado quando nada dele foi recusado.
    /// </summary>
    public static (List<GrupoColetado> Grupos, List<FieldError> Erros) ResolverGrupos(GruposLidos lidos, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(lidos);
        ArgumentNullException.ThrowIfNull(contexto);
        List<FieldError> erros = [.. lidos.Erros];
        List<GrupoColetado> grupos = [];
        for (int indice = 0; indice < lidos.Entradas.Count; indice++)
        {
            string caminho = $"grupos[{indice}]";
            (List<FatoColetado> campos, List<FieldError> dosCampos) =
                Resolver(lidos.Campos[indice] with { Erros = [] }, contexto, $"{caminho}.subitens", campoDeGrupo: true);
            erros.AddRange(dosCampos);
            if (lidos.Regras[indice] is not { } regras)
            {
                continue;
            }

            int recusasAntes = erros.Count;
            erros.AddRange(ConferenciaDoItemNoCatalogo.DoGrupo(regras.Exibicao, regras.Obrigatoriedade, contexto)
                .Select(erro => erro with { Field = string.IsNullOrEmpty(erro.Field) ? caminho : $"{caminho}.{erro.Field}" }));

            if (erros.Count > recusasAntes || !lidos.FormaValida[indice] || dosCampos.Count > 0 || lidos.Campos[indice].Erros.Count > 0)
            {
                continue;
            }

            // A forma do grupo e a identificação do candidato já foram conferidas na leitura; a
            // fábrica as repete e devolve o grupo.
            GrupoColetadoInput input = lidos.Entradas[indice];
            grupos.Add(GrupoColetado.Criar(
                input.Codigo, input.Ordem, input.EtapaCodigo, input.Rotulo, input.Minimo, input.Maximo, regras.Exibicao, regras.Obrigatoriedade, campos,
                incluiCandidato: input.IncluiCandidato).Value!);
        }

        return (grupos, erros);
    }

    /// <summary>
    /// Os itens de forma válida conferidos contra o catálogo, com as recusas da forma e da semântica
    /// no mesmo lote. O campo de grupo coleta fato de membro; o item, fato do candidato.
    /// </summary>
    public static (List<FatoColetado> Itens, List<FieldError> Erros) Resolver(
        ItensLidos lidos, ContextoDoCatalogo contexto, string caminho = "itens", bool campoDeGrupo = false)
    {
        ArgumentNullException.ThrowIfNull(lidos);
        ArgumentNullException.ThrowIfNull(contexto);
        List<FieldError> erros = [.. lidos.Erros];
        List<FatoColetado> fatos = [];
        for (int indice = 0; indice < lidos.Entradas.Count; indice++)
        {
            if (lidos.Regras[indice] is not { } regrasDoItem)
            {
                continue;
            }

            string campo = $"{caminho}[{indice}]";
            Result<FatoColetado> fato = ResolverFato(lidos.Entradas[indice], regrasDoItem, contexto, campoDeGrupo);
            if (fato.IsSuccess)
            {
                fatos.Add(fato.Value!);
            }
            else
            {
                erros.AddRange(fato.Errors.Select(erro => erro with { Field = string.IsNullOrEmpty(erro.Field) ? campo : $"{campo}.{erro.Field}" }));
            }
        }

        return (fatos, erros);
    }

    /// <summary>
    /// A obrigatoriedade do item na forma do termo: <c>SEMPRE</c> ou <c>NUNCA</c> sem predicado,
    /// <c>QUANDO</c> com ele. A semântica do predicado é conferida contra o catálogo, depois.
    /// </summary>
    private static Obrigatoriedade? ConferirObrigatoriedade(
        string? token, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicadoInput, string campo, List<FieldError> erros)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(predicadoInput);
        if (predicado.IsFailure)
        {
            erros.Add(new($"{campo}.predicadoObrigatoriedade", predicado.Error!));
            return null;
        }

        if (EntradaDeRegras.Obrigatoriedade(token, predicado.Value) is { } obrigatoriedade)
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

    private static Result<FatoColetado> ResolverFato(FatoColetadoInput input, RegrasDoItem regras, ContextoDoCatalogo contexto, bool campoDeGrupo)
    {
        // O item coleta fato declarado do próprio candidato; o campo de grupo, fato declarado de
        // membro. Derivados e calculados não se respondem.
        if ((campoDeGrupo
                ? ConferenciaNoCatalogo.FatoDoSubitem(input.FatoCodigo, contexto.FatosDasRegras)
                : ConferenciaNoCatalogo.FatoDoItem(input.FatoCodigo, contexto.FatosDasRegras)) is { } naoColetavel)
        {
            return Result<FatoColetado>.ValidationFailure([new("fatoCodigo", naoColetavel)]);
        }

        FatoCandidatoView view = contexto.Fatos[input.FatoCodigo];
        TipoRenderizacao tipoRenderizacao = TipoRenderizacaoCodigo.FromCodigo(input.TipoRenderizacao);
        if (ConferenciaDoItemNoCatalogo.Coerencia(view, tipoRenderizacao) is { } incoerencia)
        {
            return Result<FatoColetado>.ValidationFailure([new("tipoRenderizacao", incoerencia)]);
        }

        List<FieldError> erros = [];
        Result<PrecondicaoLida?> precondicao = LerPrecondicao(input.Precondicao);
        if (precondicao.IsFailure)
        {
            erros.Add(new("precondicao", precondicao.Error!));
        }

        erros.AddRange(ConferenciaDoItemNoCatalogo.Semantica(
            view, tipoRenderizacao, precondicao.IsSuccess ? precondicao.Value?.Predicado : null,
            (input.Precondicao ?? []).SelectMany(static c => c).Where(static c => c is not null).Select(static c => c.Fato),
            regras.Obrigatoriedade, regras.Restricoes, regras.Impedimento, contexto));
        bool precondicaoRecusada = erros.Any(static e => e.Field == "precondicao");

        // As recusas do próprio item (coerência das restrições com o campo, autorreferência, ajuda)
        // saem junto das semânticas, no mesmo lote.
        Result<FatoColetado> fato = FatoColetado.Criar(
            input.FatoCodigo, input.Ordem, input.Rotulo, tipoRenderizacao, regras.Obrigatoriedade,
            precondicaoRecusada ? null : precondicao.Value?.Condicoes,
            origemValores: VocabularioDeFatos.OrigemValores(view), etapaCodigo: input.EtapaCodigo, formato: view.Formato,
            ajuda: input.Ajuda, pedirConfirmacao: input.PedirConfirmacao, restricoes: regras.Restricoes, impedimento: regras.Impedimento,
            classificacaoProtecao: view.ClassificacaoProtecao);
        return erros.Count == 0
            ? fato
            : Result<FatoColetado>.ValidationFailure([.. erros, .. fato.IsFailure ? fato.Errors : []]);
    }

    /// <summary>A pré-condição de um item na forma: as condições do item e o predicado que elas formam.</summary>
    private sealed record PrecondicaoLida(IReadOnlyList<CondicaoPrecondicaoFato> Condicoes, PredicadoDnf Predicado);

    /// <summary>
    /// Monta a pré-condição de um fato pela <b>forma</b> de cada condição
    /// (<see cref="CondicaoDnf.Criar"/>). A semântica do predicado — fato citado no vocabulário,
    /// operador × domínio, valor × domínio — é de <see cref="ConferenciaDoItemNoCatalogo"/>, e a
    /// estrutura do grafo (o fato citado é coletado e anterior) é do agregado, em
    /// <see cref="ProcessoSeletivo.DefinirFatosColetados"/>.
    /// </summary>
    private static Result<PrecondicaoLida?> LerPrecondicao(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? precondicao)
    {
        if (precondicao is null)
        {
            return Result<PrecondicaoLida?>.Success(null);
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
                    return Result<PrecondicaoLida?>.Failure(new DomainError(
                        CondicaoPrecondicaoFatoErrorCodes.ClausulaInvalida,
                        "A pré-condição contém uma condição nula."));
                }

                Operador operador = OperadorCodigo.FromCodigo(condicaoInput.Operador);

                Result<CondicaoPrecondicaoFato> condicaoResult =
                    CondicaoPrecondicaoFato.Criar(clausula, condicaoInput.Fato, operador, condicaoInput.Valor);
                if (condicaoResult.IsFailure)
                {
                    return Result<PrecondicaoLida?>.Failure(condicaoResult.Error!);
                }

                condicoes.Add(condicaoResult.Value!);
                linhas.Add((clausula, CondicaoDnf.Criar(condicaoInput.Fato, operador, condicaoInput.Valor).Value!));
            }
        }

        Result<PredicadoDnf> predicadoResult = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
        if (predicadoResult.IsFailure)
        {
            return Result<PrecondicaoLida?>.Failure(predicadoResult.Error!);
        }

        return Result<PrecondicaoLida?>.Success(new PrecondicaoLida(condicoes, predicadoResult.Value!));
    }
}
