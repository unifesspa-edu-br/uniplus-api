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

/// <summary>Os itens lidos pela forma, sem leitura externa: a configuração de cada item cuja forma foi aceita.</summary>
internal sealed record ItensLidos(IReadOnlyList<FatoColetadoInput> Entradas, IReadOnlyList<ConfiguracaoDoItem?> Configuracoes, IReadOnlyList<FieldError> Erros);

/// <summary>O tipo de campo, a obrigatoriedade, as restrições e o impedimento de um item cuja forma foi aceita.</summary>
internal sealed record ConfiguracaoDoItem(TipoRenderizacao TipoRenderizacao, Obrigatoriedade Obrigatoriedade, IReadOnlyList<RestricaoValor> Restricoes, Impedimento? Impedimento = null)
{
    /// <summary>As regras do item com a exibição dada, que a leitura da exibição devolve à parte.</summary>
    public RegrasDoCampo RegrasComExibicao(PredicadoDnf? exibicao) => new(exibicao, Obrigatoriedade, Restricoes, Impedimento);
}

/// <summary>A exibição e a obrigatoriedade de um grupo cuja forma foi aceita.</summary>
internal sealed record ConfiguracaoDoGrupo(PredicadoDnf? Exibicao, Obrigatoriedade Obrigatoriedade);

/// <summary>A exibição e a obrigatoriedade lidas de um grupo, com as recusas da leitura; a parte recusada fica ausente.</summary>
internal sealed record RegrasLidasDoGrupo(PredicadoDnf? Exibicao, Obrigatoriedade? Obrigatoriedade, IReadOnlyList<FieldError> Erros)
{
    /// <summary>A configuração do grupo; nula quando alguma parte foi recusada.</summary>
    public ConfiguracaoDoGrupo? Configuracao => Erros.Count == 0 ? new ConfiguracaoDoGrupo(Exibicao, Obrigatoriedade!) : null;
}

/// <summary>
/// Um grupo lido pela forma, sem leitura externa: a configuração dele, se a forma inteira foi
/// aceita, os campos lidos como os itens e todas as recusas da leitura, as do grupo e as dos campos.
/// </summary>
internal sealed record GrupoLido(
    GrupoColetadoInput Entrada, ConfiguracaoDoGrupo? Configuracao, bool FormaValida, ItensLidos Campos, IReadOnlyList<FieldError> Erros);

/// <summary>Os grupos lidos pela forma, com as recusas da leitura de todos.</summary>
internal sealed record GruposLidos(IReadOnlyList<GrupoLido> Grupos, IReadOnlyList<FieldError> Erros);

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
        ConfiguracaoDoItem?[] configuracoes = new ConfiguracaoDoItem?[entradas.Count];
        for (int indice = 0; indice < entradas.Count; indice++)
        {
            configuracoes[indice] = LerConfiguracao(entradas[indice], $"{caminho}[{indice}]", erros);
        }

        return new ItensLidos(entradas, configuracoes, erros);
    }

    /// <summary>
    /// A configuração do item pela forma, acumulando as recusas dele em <paramref name="erros"/>; nula quando o item teve
    /// alguma recusa. A coerência do tipo de campo com o fato só roda com o tipo lido, depois, contra o catálogo.
    /// </summary>
    private static ConfiguracaoDoItem? LerConfiguracao(FatoColetadoInput input, string campo, List<FieldError> erros)
    {
        int recusasAntes = erros.Count;
        erros.AddRange(FormaDoItem.ValidarFormaBasica(input.FatoCodigo, input.Ordem, input.Apresentacao?.Rotulo).Select(erro => erro.NoCaminhoDe(campo)));
        TipoRenderizacao? tipo = LerTipoRenderizacao(input.Apresentacao?.TipoRenderizacao, $"{campo}.{FatoColetadoInput.CaminhoDaApresentacao}", erros);
        (Obrigatoriedade? obrigatoriedade, IReadOnlyList<RestricaoValor> restricoes, Impedimento? impedimento) =
            LerRegras(input.Regras, $"{campo}.{FatoColetadoInput.CaminhoDasRegras}", erros);
        return erros.Count == recusasAntes ? new ConfiguracaoDoItem(tipo!.Value, obrigatoriedade!, restricoes, impedimento) : null;
    }

    /// <summary>
    /// A obrigatoriedade, as restrições e o impedimento do item pela forma, no caminho das regras. A exibição é lida à parte, na
    /// resolução, porque a recusa dela sai junto das semânticas.
    /// </summary>
    private static (Obrigatoriedade? Obrigatoriedade, IReadOnlyList<RestricaoValor> Restricoes, Impedimento? Impedimento) LerRegras(
        RegrasDoCampoInput? regras, string campo, List<FieldError> erros)
    {
        Obrigatoriedade? obrigatoriedade = ConferirObrigatoriedade(
            regras?.Obrigatoriedade?.Tipo, regras?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado", erros);
        IReadOnlyList<RestricaoValor> restricoes = ConferirRestricoes(regras?.Restricoes, campo, erros);
        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(regras?.Impedimento);
        if (impedimento.IsFailure)
        {
            erros.Add(new($"{campo}.impedimento.quando", impedimento.Error!));
        }

        return (obrigatoriedade, restricoes, impedimento.Value);
    }

    private static TipoRenderizacao? LerTipoRenderizacao(string? codigo, string campo, List<FieldError> erros)
    {
        Result<TipoRenderizacao> tipo = TipoRenderizacaoDaEntrada.Ler(codigo);
        if (tipo.IsFailure)
        {
            erros.Add(new($"{campo}.tipoRenderizacao", tipo.Error!));
        }

        return tipo.IsSuccess ? tipo.Value : null;
    }

    /// <summary>
    /// Os grupos pela forma, sem leitura externa: a exibição e a obrigatoriedade de cada um, a forma
    /// do grupo — código, ordem, rótulo, contagem, quantidade de campos, autorreferência — e os
    /// campos, lidos como os itens no caminho do grupo, sem seção própria.
    /// </summary>
    public static GruposLidos LerGrupos(IReadOnlyList<GrupoColetadoInput> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        GrupoLido[] grupos = [.. entradas.Select((entrada, indice) => LerGrupo(entrada, $"grupos[{indice}]"))];
        return new GruposLidos(grupos, [.. grupos.SelectMany(static g => g.Erros)]);
    }

    private static GrupoLido LerGrupo(GrupoColetadoInput input, string caminho)
    {
        RegrasLidasDoGrupo regras = LerRegrasDoGrupo(input, caminho);

        // A identificação da ocorrência do candidato depende só do mínimo e da forma lida dos
        // campos, e sai no mesmo lote das demais recusas, mesmo com campo recusado (ADR-0125).
        ItensLidos campos = Ler(input.Subitens ?? [], $"{caminho}.subitens");
        List<FieldError> erros =
        [
            .. regras.Erros,
            .. ConferirFormaDoGrupo(input, regras, caminho),
            .. input.IncluiCandidato ? NoCaminho(ConferirCandidatoComoMembro(input.Minimo, input.Subitens ?? [], campos), caminho) : [],
        ];
        return new GrupoLido(input, regras.Configuracao, erros.Count == 0, campos, [.. erros, .. campos.Erros]);
    }

    private static RegrasLidasDoGrupo LerRegrasDoGrupo(GrupoColetadoInput input, string caminho)
    {
        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(input.Exibicao);
        List<FieldError> erros = [];
        if (exibicao.IsFailure)
        {
            erros.Add(new($"{caminho}.exibicao", exibicao.Error!));
        }

        Obrigatoriedade? obrigatoriedade = ConferirObrigatoriedade(
            input.Obrigatoriedade, input.PredicadoObrigatoriedade, $"{caminho}.obrigatoriedade", $"{caminho}.predicadoObrigatoriedade", erros);
        return new RegrasLidasDoGrupo(exibicao.IsSuccess ? exibicao.Value : null, obrigatoriedade, erros);
    }

    private static IEnumerable<FieldError> ConferirFormaDoGrupo(GrupoColetadoInput input, RegrasLidasDoGrupo regras, string caminho) =>
        NoCaminho(
            FormaDoGrupo.Conferir(
                input.Codigo, input.Ordem, input.Rotulo, new OcorrenciasDoGrupo(input.Minimo, input.Maximo),
                [.. (input.Subitens ?? []).Select(static s => (s?.FatoCodigo, s?.EtapaCodigo, s?.Regras?.Impedimento is not null))],
                regras.Exibicao?.FatosCitados ?? [],
                regras.Obrigatoriedade ?? Obrigatoriedade.Nunca),
            caminho);

    private static IEnumerable<FieldError> NoCaminho(IEnumerable<FieldError> erros, string caminho) =>
        erros.Select(erro => erro with { Field = $"{caminho}.{erro.Field}" });

    /// <summary>
    /// O candidato como membro pela forma lida dos campos. O campo de forma recusada já tem a
    /// própria recusa e conta aqui como conforme, para que só a falta do parentesco seja acusada.
    /// </summary>
    private static List<FieldError> ConferirCandidatoComoMembro(int minimo, IReadOnlyList<FatoColetadoInput> subitens, ItensLidos lidos) =>
        CandidatoComoMembro.Conferir(minimo, [.. subitens.Select((subitem, indice) => lidos.Configuracoes[indice] is { } configuracao
            ? (subitem.FatoCodigo, subitem.Regras?.Exibicao is { Count: > 0 }, configuracao.Obrigatoriedade, configuracao.Restricoes)
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
        List<(GrupoColetado? Grupo, List<FieldError> Erros)> resolvidos =
            [.. lidos.Grupos.Select((grupo, indice) => ResolverGrupo(grupo, $"grupos[{indice}]", contexto))];
        return ([.. resolvidos.Select(static r => r.Grupo).OfType<GrupoColetado>()], [.. lidos.Erros, .. resolvidos.SelectMany(static r => r.Erros)]);
    }

    private static (GrupoColetado? Grupo, List<FieldError> Erros) ResolverGrupo(GrupoLido lido, string caminho, ContextoDoCatalogo contexto)
    {
        (List<FatoColetado> campos, List<FieldError> dosCampos) = Resolver(lido.Campos with { Erros = [] }, contexto, $"{caminho}.subitens", campoDeGrupo: true);
        if (lido.Configuracao is not { } configuracao)
        {
            return (null, dosCampos);
        }

        List<FieldError> doGrupo = [.. ConferenciaDoItemNoCatalogo.DoGrupo(configuracao.Exibicao, configuracao.Obrigatoriedade, contexto).Select(erro => erro.NoCaminhoDe(caminho))];
        bool recusado = doGrupo.Count > 0 || !lido.FormaValida || dosCampos.Count > 0 || lido.Campos.Erros.Count > 0;
        return (recusado ? null : CriarGrupo(lido.Entrada, configuracao, campos), [.. dosCampos, .. doGrupo]);
    }

    // A forma do grupo e a identificação do candidato já foram conferidas na leitura; a fábrica as
    // repete e devolve o grupo.
    private static GrupoColetado CriarGrupo(GrupoColetadoInput input, ConfiguracaoDoGrupo configuracao, List<FatoColetado> campos) =>
        GrupoColetado.Criar(
            input.Codigo, input.Ordem, input.EtapaCodigo, input.Rotulo, input.Minimo, input.Maximo, configuracao.Exibicao, configuracao.Obrigatoriedade, campos,
            incluiCandidato: input.IncluiCandidato).Value!;

    /// <summary>
    /// Os itens de forma válida conferidos contra o catálogo, com as recusas da forma e da semântica
    /// no mesmo lote. O campo de grupo coleta fato de membro; o item, fato do candidato.
    /// </summary>
    public static (List<FatoColetado> Itens, List<FieldError> Erros) Resolver(
        ItensLidos lidos, ContextoDoCatalogo contexto, string caminho = "itens", bool campoDeGrupo = false)
    {
        ArgumentNullException.ThrowIfNull(lidos);
        ArgumentNullException.ThrowIfNull(contexto);
        List<(int Indice, Result<FatoColetado> Fato)> resolvidos =
        [
            .. lidos.Configuracoes.Select((configuracao, indice) => (indice, configuracao))
                .Where(static item => item.configuracao is not null)
                .Select(item => (item.indice, ResolverFato(lidos.Entradas[item.indice], item.configuracao!, contexto, campoDeGrupo))),
        ];
        return (
            [.. resolvidos.Where(static r => r.Fato.IsSuccess).Select(static r => r.Fato.Value!)],
            [.. lidos.Erros, .. resolvidos.Where(static r => r.Fato.IsFailure).SelectMany(r => r.Fato.Errors.Select(erro => erro.NoCaminhoDe($"{caminho}[{r.Indice}]")))]);
    }

    /// <summary>
    /// A obrigatoriedade na forma do termo: <c>SEMPRE</c> ou <c>NUNCA</c> sem predicado,
    /// <c>QUANDO</c> com ele. A semântica do predicado é conferida contra o catálogo, depois.
    /// </summary>
    private static Obrigatoriedade? ConferirObrigatoriedade(
        string? token,
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicadoInput,
        string caminhoDoToken,
        string caminhoDoPredicado,
        List<FieldError> erros)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(predicadoInput);
        Obrigatoriedade? obrigatoriedade = predicado.IsSuccess ? EntradaDeRegras.Obrigatoriedade(token, predicado.Value) : null;
        erros.AddRange(predicado.IsFailure
            ? [new FieldError(caminhoDoPredicado, predicado.Error!)]
            : obrigatoriedade is null ? [new FieldError(caminhoDoToken, ObrigatoriedadeInvalida)] : []);
        return obrigatoriedade;
    }

    private static readonly DomainError ObrigatoriedadeInvalida = new(
        FatoColetadoErrorCodes.ObrigatoriedadeInvalida, "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.");

    /// <summary>A forma das restrições de valor do item; os valores e fatos citados são conferidos contra o catálogo, depois.</summary>
    private static List<RestricaoValor> ConferirRestricoes(IReadOnlyList<RestricaoValorInput>? restricoes, string campo, List<FieldError> erros)
    {
        List<Result<RestricaoValor>> lidas = [.. (restricoes ?? []).Select(LerRestricao)];
        erros.AddRange(lidas.Select((lida, indice) => (lida, indice)).Where(static r => r.lida.IsFailure)
            .Select(r => new FieldError($"{campo}.restricoes[{r.indice}]", r.lida.Error!)));
        return [.. lidas.Where(static lida => lida.IsSuccess).Select(static lida => lida.Value!)];
    }

    private static Result<RestricaoValor> LerRestricao(RestricaoValorInput? entrada) =>
        entrada is null
            ? Result<RestricaoValor>.Failure(new DomainError(RestricaoValorErrorCodes.TipoDesconhecido, "A restrição de valor é nula."))
            : EntradaDeRegras.Restricao(entrada);

    private static Result<FatoColetado> ResolverFato(FatoColetadoInput input, ConfiguracaoDoItem configuracao, ContextoDoCatalogo contexto, bool campoDeGrupo)
    {
        Result<FatoCandidatoView> fato = FatoColetavelNoCampo(input, configuracao.TipoRenderizacao, contexto, campoDeGrupo);
        if (fato.IsFailure)
        {
            return Result<FatoColetado>.ValidationFailure(fato.Errors);
        }

        (RegrasDoCampo regras, List<FieldError> erros) = ConferirSemanticaDasRegras(input, fato.Value!, configuracao, contexto);
        return CriarComAsCopiasDoCatalogo(input, configuracao.TipoRenderizacao, regras, fato.Value!, erros);
    }

    /// <summary>
    /// O fato que o campo coleta, do catálogo: o item coleta fato declarado do próprio candidato; o
    /// campo de grupo, fato declarado de membro, e derivados e calculados não se respondem. O tipo
    /// de campo tem de ser coerente com o fato.
    /// </summary>
    private static Result<FatoCandidatoView> FatoColetavelNoCampo(
        FatoColetadoInput input, TipoRenderizacao tipoRenderizacao, ContextoDoCatalogo contexto, bool campoDeGrupo)
    {
        DomainError? naoColetavel = campoDeGrupo
            ? ConferenciaNoCatalogo.FatoDoSubitem(input.FatoCodigo, contexto.FatosDasRegras)
            : ConferenciaNoCatalogo.FatoDoItem(input.FatoCodigo, contexto.FatosDasRegras);
        if (naoColetavel is not null)
        {
            return Result<FatoCandidatoView>.ValidationFailure([new("fatoCodigo", naoColetavel)]);
        }

        FatoCandidatoView fato = contexto.Fatos[input.FatoCodigo];
        return ConferenciaDoItemNoCatalogo.Coerencia(fato, tipoRenderizacao) is { } incoerencia
            ? Result<FatoCandidatoView>.ValidationFailure([new($"{FatoColetadoInput.CaminhoDaApresentacao}.tipoRenderizacao", incoerencia)])
            : Result<FatoCandidatoView>.Success(fato);
    }

    /// <summary>
    /// A exibição lida da entrada e a semântica de todas as regras do item contra o catálogo. Devolve
    /// as regras com que o item é criado — sem a exibição, quando ela foi recusada, para que as recusas
    /// do próprio item saiam junto das demais — e todas as recusas.
    /// </summary>
    private static (RegrasDoCampo Regras, List<FieldError> Erros) ConferirSemanticaDasRegras(
        FatoColetadoInput input, FatoCandidatoView fato, ConfiguracaoDoItem configuracao, ContextoDoCatalogo contexto)
    {
        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(input.Regras?.Exibicao);
        RegrasDoCampo lidas = configuracao.RegrasComExibicao(exibicao.IsSuccess ? exibicao.Value : null);
        IEnumerable<string> fatosDaExibicao = (input.Regras?.Exibicao ?? []).SelectMany(static c => c).Where(static c => c is not null).Select(static c => c.Fato);
        List<FieldError> erros =
        [
            .. exibicao.Errors.Select(static e => new FieldError($"{ConferenciaDoItemNoCatalogo.CaminhoDaExibicao}{e.Field}", e.Error)),
            .. ConferenciaDoItemNoCatalogo.Semantica(fato, configuracao.TipoRenderizacao, lidas, fatosDaExibicao, contexto),
        ];
        bool exibicaoRecusada = erros.Any(static e => e.Field is { } campo && campo.StartsWith(ConferenciaDoItemNoCatalogo.CaminhoDaExibicao, StringComparison.Ordinal));
        return (exibicaoRecusada ? configuracao.RegrasComExibicao(null) : lidas, erros);
    }

    /// <summary>
    /// O item criado com as cópias tiradas do catálogo. As recusas do próprio item (coerência das
    /// restrições com o campo, autorreferência, ajuda) saem junto das semânticas, no mesmo lote.
    /// </summary>
    private static Result<FatoColetado> CriarComAsCopiasDoCatalogo(
        FatoColetadoInput input, TipoRenderizacao tipoRenderizacao, RegrasDoCampo regras, FatoCandidatoView fato, List<FieldError> erros)
    {
        Result<FatoColetado> item = FatoColetado.Criar(
            input.FatoCodigo,
            input.Ordem,
            input.EtapaCodigo,
            EntradaDeRegras.Apresentacao(input.Apresentacao, tipoRenderizacao),
            regras,
            VocabularioDeFatos.CopiaNoCatalogo(fato));
        return erros.Count == 0
            ? item
            : Result<FatoColetado>.ValidationFailure([.. erros, .. item.IsFailure ? item.Errors : []]);
    }
}
