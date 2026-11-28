namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using CampoDoMembro = (string FatoCodigo, bool TemExibicao, Unifesspa.UniPlus.Regras.Formularios.Obrigatoriedade Obrigatoriedade, System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Formularios.RestricaoValor> Restricoes);

/// <summary>Um item lido pela forma: o que foi enviado, as regras que a leitura aceitou e a configuração, nula quando o item foi recusado.</summary>
internal sealed record ItemLido(FatoColetadoInput Entrada, RegrasLidasDoCampo Regras, ConfiguracaoDoItem? Configuracao);

/// <summary>Os itens lidos pela forma, sem leitura externa, e todas as recusas da leitura deles.</summary>
internal sealed record ItensLidos(IReadOnlyList<ItemLido> Itens, IReadOnlyList<FieldError> Erros)
{
    /// <summary>Os códigos de dado enviados, inclusive os dos itens recusados; o código em branco fica de fora.</summary>
    public IEnumerable<string> CodigosEnviados =>
        Itens.Select(static item => item.Entrada.FatoCodigo).Where(static codigo => !string.IsNullOrWhiteSpace(codigo));

    /// <summary>As condições das regras que a leitura aceitou, inclusive as dos itens recusados.</summary>
    public IEnumerable<CondicaoDnf> CondicoesLidas =>
        Itens.SelectMany(static item => item.Regras.CondicoesDoCampo(item.Entrada.FatoCodigo));
}

/// <summary>O tipo de campo, a obrigatoriedade, as restrições e o impedimento de um item cuja leitura foi aceita.</summary>
internal sealed record ConfiguracaoDoItem(TipoDeCampo TipoDeCampo, Obrigatoriedade Obrigatoriedade, IReadOnlyList<RestricaoValor> Restricoes, Impedimento? Impedimento = null)
{
    /// <summary>As regras do item com a exibição dada, que a leitura da exibição devolve à parte.</summary>
    public RegrasDoCampo RegrasComExibicao(PredicadoDnf? exibicao) => new(exibicao, Obrigatoriedade, Restricoes, Impedimento);

    /// <summary>A configuração de um item cujas regras foram todas lidas.</summary>
    public static ConfiguracaoDoItem De(TipoDeCampo tipoDeCampo, RegrasLidasDoCampo regras) =>
        new(tipoDeCampo, regras.Obrigatoriedade!, [.. regras.Restricoes.OfType<RestricaoValor>()], regras.Impedimento);
}

/// <summary>A exibição e a obrigatoriedade lidas de um grupo, com as recusas da leitura; a parte recusada fica ausente.</summary>
internal sealed record RegrasLidasDoGrupo(PredicadoDnf? Exibicao, Obrigatoriedade? Obrigatoriedade, IReadOnlyList<FieldError> Erros)
{
    /// <summary>As regras do grupo; nulas quando alguma parte foi recusada.</summary>
    public ExibicaoEObrigatoriedade? Configuracao => Erros.Count == 0 ? ParaAConferenciaDaForma : null;

    /// <summary>As regras lidas, com <c>Nunca</c> no lugar da obrigatoriedade recusada, para a forma do grupo ser conferida mesmo assim.</summary>
    public ExibicaoEObrigatoriedade ParaAConferenciaDaForma => ExibicaoEObrigatoriedade.ParaConferirAForma(Exibicao, Obrigatoriedade);
}

/// <summary>
/// Um grupo lido pela forma, sem leitura externa: as ocorrências, nulas quando a leitura delas foi recusada, as regras
/// dele, se a forma inteira foi aceita, os campos lidos como os itens e todas as recusas da leitura, as do grupo e as dos
/// campos.
/// </summary>
internal sealed record GrupoLido(
    GrupoColetadoInput Entrada,
    OcorrenciasDoGrupo? Ocorrencias,
    RegrasLidasDoGrupo Regras,
    bool FormaValida,
    ItensLidos Campos,
    IReadOnlyList<FieldError> Erros)
{
    /// <summary>As regras do grupo, se a leitura delas foi aceita.</summary>
    public ExibicaoEObrigatoriedade? Configuracao => Regras.Configuracao;
}

/// <summary>Os grupos lidos pela forma, com as recusas da leitura de todos.</summary>
internal sealed record GruposLidos(IReadOnlyList<GrupoLido> Grupos, IReadOnlyList<FieldError> Erros)
{
    /// <summary>Os códigos de dado enviados nos campos dos grupos, inclusive os dos campos recusados.</summary>
    public IEnumerable<string> CodigosEnviados => Grupos.SelectMany(static grupo => grupo.Campos.CodigosEnviados);

    /// <summary>As condições lidas das regras dos grupos e dos campos deles, inclusive as dos recusados.</summary>
    public IEnumerable<CondicaoDnf> CondicoesLidas =>
        Grupos.SelectMany(static grupo => grupo.Regras.ParaAConferenciaDaForma.Condicoes.Concat(grupo.Campos.CondicoesLidas));
}

/// <summary>
/// A leitura dos itens e dos grupos do formulário do processo pela forma, sem ler nem mudar o processo. O item cuja
/// leitura foi recusada passa por toda regra de forma que não depende do que foi recusado, e as recusas saem juntas, cada
/// uma uma vez (ADR-0125). O catálogo entra só pelo formato do dado.
/// </summary>
internal static class LeituraDosItensDoFormulario
{
    public static ItensLidos Ler(IReadOnlyList<FatoColetadoInput> entradas, ContextoDoCatalogo contexto, string caminho = "itens")
    {
        ArgumentNullException.ThrowIfNull(entradas);
        ArgumentNullException.ThrowIfNull(contexto);
        List<FieldError> erros = [];
        List<ItemLido> itens = [];
        for (int indice = 0; indice < entradas.Count; indice++)
        {
            itens.Add(LerItem(entradas[indice], contexto, $"{caminho}[{indice}]", erros));
        }

        return new ItensLidos(itens, erros);
    }

    /// <summary>
    /// O item lido, acumulando em <paramref name="erros"/> as recusas dele quando alguma parte foi recusada: as da leitura e
    /// as da forma que não dependem do que foi recusado. O item aceito não emite nada aqui; a forma dele é conferida ao criá-lo.
    /// </summary>
    private static ItemLido LerItem(FatoColetadoInput entrada, ContextoDoCatalogo contexto, string campo, List<FieldError> erros)
    {
        (TipoDeCampo? tipo, List<FieldError> doTipo) = LerTipoDeCampo(entrada.Apresentacao?.TipoDeCampo, $"{campo}.{FatoColetadoInput.CaminhoDaApresentacao}");
        (PredicadoDnf? exibicao, List<FieldError> daExibicao) = LerExibicao(entrada.Regras?.Exibicao, $"{campo}.{ConferenciaDoItemNoCatalogo.CaminhoDaExibicao}");
        (RegrasLidasDoCampo regras, List<FieldError> dasRegras) = LerRegras(entrada.Regras, exibicao, $"{campo}.{FatoColetadoInput.CaminhoDasRegras}");
        bool recusado = doTipo.Count > 0 || dasRegras.Count > 0
            || FormaDoItem.ValidarFormaBasica(entrada.FatoCodigo, entrada.Ordem, entrada.Apresentacao?.Rotulo).Count > 0;
        if (!recusado)
        {
            return new ItemLido(entrada, regras, ConfiguracaoDoItem.De(tipo!.Value, regras));
        }

        string? formato = contexto.FormatoDoDado(entrada.FatoCodigo);
        erros.AddRange([.. doTipo, .. daExibicao, .. dasRegras, .. FormaDoItemNaEntrada.Conferir(entrada, tipo, formato, regras).Select(erro => erro.NoCaminhoDoItem(campo))]);
        return new ItemLido(entrada, regras, null);
    }

    private static (TipoDeCampo? Tipo, List<FieldError> Erros) LerTipoDeCampo(string? codigo, string campo)
    {
        Result<TipoDeCampo> tipo = TipoDeCampoDaEntrada.Ler(codigo);
        return tipo.IsSuccess ? (tipo.Value, []) : (null, [new($"{campo}.tipoDeCampo", tipo.Error!)]);
    }

    private static (PredicadoDnf? Exibicao, List<FieldError> Erros) LerExibicao(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? entrada, string campo)
    {
        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(entrada);
        return (exibicao.IsSuccess ? exibicao.Value : null, [.. exibicao.Errors.Select(erro => erro.NoCaminhoDoItem(campo))]);
    }

    /// <summary>A obrigatoriedade, as restrições e o impedimento do item pela forma, no caminho das regras, com a exibição já lida.</summary>
    private static (RegrasLidasDoCampo Regras, List<FieldError> Erros) LerRegras(RegrasDoCampoInput? regras, PredicadoDnf? exibicao, string campo)
    {
        List<FieldError> erros = [];
        Obrigatoriedade? obrigatoriedade = ConferirObrigatoriedade(
            regras?.Obrigatoriedade?.Tipo, regras?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado", erros);
        IReadOnlyList<RestricaoValor?> restricoes = ConferirRestricoes(regras?.Restricoes, campo, erros);
        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(regras?.Impedimento);
        erros.AddRange(impedimento.Errors.Select(erro => erro.NoCaminhoDoItem($"{campo}.impedimento.quando")));
        return (new RegrasLidasDoCampo(exibicao, obrigatoriedade, restricoes, impedimento.Value), erros);
    }

    /// <summary>
    /// Os grupos pela forma, sem leitura externa: a exibição e a obrigatoriedade de cada um, a forma do grupo — código,
    /// ordem, rótulo, contagem, quantidade de campos, autorreferência — e os campos, lidos como os itens no caminho do
    /// grupo, sem seção própria.
    /// </summary>
    public static GruposLidos LerGrupos(IReadOnlyList<GrupoColetadoInput> entradas, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        ArgumentNullException.ThrowIfNull(contexto);
        GrupoLido[] grupos = [.. entradas.Select((entrada, indice) => LerGrupo(entrada, contexto, $"grupos[{indice}]"))];
        return new GruposLidos(grupos, [.. grupos.SelectMany(static g => g.Erros)]);
    }

    private static GrupoLido LerGrupo(GrupoColetadoInput input, ContextoDoCatalogo contexto, string caminho)
    {
        OcorrenciasLidasDoGrupo ocorrencias = OcorrenciasLidasDoGrupo.Ler(input.Ocorrencias);
        RegrasLidasDoGrupo regras = LerRegrasDoGrupo(input, caminho);
        ItensLidos campos = Ler(input.Subitens ?? [], contexto, $"{caminho}.subitens");
        List<FieldError> erros = [.. NoCaminho(ocorrencias.Recusas, caminho), .. regras.Erros, .. RecusasDaFormaDoGrupo(input, ocorrencias, regras, campos, caminho)];
        return new GrupoLido(input, ocorrencias.Declaradas, regras, erros.Count == 0, campos, [.. erros, .. campos.Erros]);
    }

    // A identificação da ocorrência do candidato depende da inclusão declarada, do mínimo quando declarado e da
    // forma lida dos campos; as recusas são acumuladas na mesma resposta (ADR-0125).
    private static IEnumerable<FieldError> RecusasDaFormaDoGrupo(
        GrupoColetadoInput input, OcorrenciasLidasDoGrupo ocorrencias, RegrasLidasDoGrupo regras, ItensLidos campos, string caminho) =>
        NoCaminho(FormaDoGrupoNaEntrada.Conferir(input, ocorrencias, regras.Exibicao, regras.Obrigatoriedade, CamposDoMembro(input, campos)), caminho);

    private static RegrasLidasDoGrupo LerRegrasDoGrupo(GrupoColetadoInput input, string caminho)
    {
        string caminhoDasRegras = $"{caminho}.{GrupoColetadoInput.CaminhoDasRegras}";
        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(input.Regras?.Exibicao);
        List<FieldError> erros = [.. exibicao.Errors.Select(e => e.NoCaminhoDoItem($"{caminhoDasRegras}.exibicao"))];
        Obrigatoriedade? obrigatoriedade = ConferirObrigatoriedade(
            input.Regras?.Obrigatoriedade?.Tipo, input.Regras?.Obrigatoriedade?.Predicado,
            $"{caminhoDasRegras}.obrigatoriedade", $"{caminhoDasRegras}.obrigatoriedade.predicado", erros);
        return new RegrasLidasDoGrupo(exibicao.IsSuccess ? exibicao.Value : null, obrigatoriedade, erros);
    }

    private static IEnumerable<FieldError> NoCaminho(IEnumerable<FieldError> erros, string caminho) =>
        erros.Select(erro => erro.NoCaminhoDoItem(caminho));

    /// <summary>
    /// Os campos pela forma lida, para identificar o candidato como membro. O campo de forma recusada já tem a
    /// própria recusa e fica nulo, para que só a falta do parentesco seja acusada.
    /// </summary>
    private static CampoDoMembro?[] CamposDoMembro(GrupoColetadoInput input, ItensLidos lidos) =>
        [.. (input.Subitens ?? []).Select((subitem, indice) => lidos.Itens[indice].Configuracao is { } configuracao
            ? (CampoDoMembro?)(subitem.FatoCodigo, subitem.Regras?.Exibicao is { Count: > 0 }, configuracao.Obrigatoriedade, configuracao.Restricoes)
            : null)];

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
            ? [.. predicado.Errors.Select(e => e.NoCaminhoDoItem(caminhoDoPredicado))]
            : obrigatoriedade is null ? [new FieldError(caminhoDoToken, ObrigatoriedadeInvalida)] : []);
        return obrigatoriedade;
    }

    private static readonly DomainError ObrigatoriedadeInvalida = new(
        FatoColetadoErrorCodes.ObrigatoriedadeInvalida, "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.");

    /// <summary>
    /// A forma das restrições de valor do item, na ordem do pedido; a recusada fica nula. Os valores e os dados citados são
    /// conferidos contra o catálogo, depois.
    /// </summary>
    private static List<RestricaoValor?> ConferirRestricoes(IReadOnlyList<RestricaoValorInput>? restricoes, string campo, List<FieldError> erros)
    {
        List<Result<RestricaoValor>> lidas = [.. (restricoes ?? []).Select(EntradaDeRegras.Restricao)];
        erros.AddRange(lidas.SelectMany((lida, indice) => lida.Errors.Select(e => e.NoCaminhoDoItem($"{campo}.restricoes[{indice}]"))));
        return [.. lidas.Select(static lida => lida.IsSuccess ? lida.Value : null)];
    }
}
