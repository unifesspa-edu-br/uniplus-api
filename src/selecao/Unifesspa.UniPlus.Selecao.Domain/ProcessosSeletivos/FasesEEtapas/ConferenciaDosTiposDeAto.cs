namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os tipos de ato que o cronograma declara nos produtos das fases e das etapas: o tipo que é resultado declara papel, o que
/// congela a configuração não é item de resultado, e o tipo único por objeto sai uma só vez no processo. A classificação dos
/// tipos é dado do catálogo vigente e chega pronta.
/// </summary>
public static class ConferenciaDosTiposDeAto
{
    public static IReadOnlyList<FieldError> ConferirResultadosSemPapel(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
        ViolacoesDoProduto(cronograma, etapas, tipos, static (classificacao, produto) =>
            classificacao.ViolacaoDoResultadoSemPapel(produto.AtoCodigo, produto.Papel, produto.Lugar));

    public static IReadOnlyList<FieldError> ConferirTiposQueCongelamComoResultado(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
        ViolacoesDoProduto(cronograma, etapas, tipos, static (classificacao, produto) =>
            classificacao.ViolacaoDoTipoQueCongelaComPapel(produto.AtoCodigo, produto.Papel, produto.Lugar));

    /// <summary>
    /// O tipo único declarado mais de uma vez, contando os produtos das fases e das etapas e a vaga que o ato da operação e os
    /// atos já registrados já ocupam: o ato da retificação continua a própria linhagem, e por isso conta uma vez só. Cada produto
    /// repetido, da segunda ocorrência em diante, é uma causa no campo dele; quando a segunda ocorrência é só a vaga já
    /// ocupada, a causa está no ato pedido e não no documento, e fica sem campo.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirTiposUnicosRepetidos(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        return
        [
            .. OcorrenciasDeTipoUnico(cronograma, etapas, tipos)
                .GroupBy(static ocorrencia => ocorrencia.AtoCodigo, StringComparer.Ordinal)
                .Where(static grupo => grupo.Count() > 1)
                .SelectMany(grupo => CausasDoTipoRepetido(tipos.NomeDe(grupo.Key), [.. grupo])),
        ];
    }

    public static IReadOnlyList<ItemConformidade> Itens(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
    [
        Item(
            "cronograma_resultado_declarado_com_papel",
            "Cronograma: o tipo de ato classificado como resultado declara papel preliminar ou definitivo",
            ConferirResultadosSemPapel(cronograma, etapas, tipos).Count),
        Item(
            "cronograma_tipo_que_congela_nao_eh_resultado",
            "Cronograma: o tipo de ato que congela a configuração não é declarado como item de resultado",
            ConferirTiposQueCongelamComoResultado(cronograma, etapas, tipos).Count),
        Item(
            "cronograma_tipo_de_ato_unico_declarado_uma_vez",
            "Cronograma: o tipo de ato que só sai uma vez por processo é declarado em um único produto",
            ConferirTiposUnicosRepetidos(cronograma, etapas, tipos).Count),
    ];

    internal static DomainError TipoUnicoRepetido(string nomeDoTipo, IEnumerable<string> lugares) => new(
        ProcessoSeletivoErrorCodes.TipoDeAtoUnicoDeclaradoMaisDeUmaVez,
        $"O tipo de ato '{nomeDoTipo}' só pode ser publicado uma vez por processo e está declarado {OndeEstaDeclarado(lugares)}.");

    // Os produtos primeiro, na posição de entrada, e depois as vagas já ocupadas, que não têm campo no documento.
    private static IEnumerable<OcorrenciaDoTipo> OcorrenciasDeTipoUnico(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
    [
        .. ProdutosDeclarados(cronograma, etapas)
            .Where(produto => tipos.EhUnico(produto.AtoCodigo))
            .Select(static produto => new OcorrenciaDoTipo(produto.AtoCodigo, produto.Lugar, produto.CampoDoAto)),
        .. TiposDeAtoDoCronograma.VagasJaReivindicadas(tipos)
            .Select(static vaga => new OcorrenciaDoTipo(vaga.AtoCodigo, vaga.Lugar, null)),
    ];

    private static IEnumerable<FieldError> CausasDoTipoRepetido(string nomeDoTipo, List<OcorrenciaDoTipo> ocorrencias)
    {
        DomainError erro = TipoUnicoRepetido(nomeDoTipo, ocorrencias.Select(static ocorrencia => ocorrencia.Lugar));
        List<string> camposRepetidos = [.. ocorrencias.Skip(1).Select(static ocorrencia => ocorrencia.Campo).OfType<string>()];

        return camposRepetidos.Count == 0
            ? [new FieldError(null, erro)]
            : camposRepetidos.Select(campo => new FieldError(campo, erro));
    }

    private static string OndeEstaDeclarado(IEnumerable<string> lugares) =>
        string.Join(" e ", lugares
            .GroupBy(static lugar => lugar, StringComparer.Ordinal)
            .Select(static lugar => lugar.Count() > 1 ? $"{lugar.Key} ({lugar.Count()} vezes)" : lugar.Key));

    private static List<FieldError> ViolacoesDoProduto(
        CronogramaDoProcesso cronograma,
        EtapasDoProcesso etapas,
        TiposDeAtoUnicosPorObjeto tipos,
        Func<ClassificacaoDoTipoDeAto, ProdutoDeclarado, DomainError?> violacao)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        return
        [
            .. ProdutosDeclarados(cronograma, etapas)
                .Select(produto => (produto, erro: tipos.ClassificacaoDe(produto.AtoCodigo) is { } classificacao ? violacao(classificacao, produto) : null))
                .Where(static par => par.erro is not null)
                .Select(static par => new FieldError(par.produto.Campo, par.erro!)),
        ];
    }

    // Os produtos das fases primeiro, depois os das etapas, cada um com o lugar dele no documento.
    private static IEnumerable<ProdutoDeclarado> ProdutosDeclarados(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return
        [
            .. cronograma.Fases.SelectMany(static (fase, posicao) => fase.Atos.Produtos.Select(produto => new ProdutoDeclarado(
                produto.AtoCodigo, produto.Papel, LugarDaDeclaracao.DaFase(fase.Codigo), $"fases[{posicao}].produtos[{produto.Posicao}]"))),
            .. etapas.Etapas.SelectMany(static (etapa, posicao) => etapa.Atos.Produtos.Select(produto => new ProdutoDeclarado(
                produto.AtoCodigo, produto.Papel, LugarDaDeclaracao.DaEtapa(etapa.Nome, etapa.Ordem), $"etapas[{posicao}].produtos[{produto.Posicao}]"))),
        ];
    }

    private static ItemConformidade Item(string codigo, string mensagem, int causas) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, causas == 0);

    private sealed record OcorrenciaDoTipo(string AtoCodigo, string Lugar, string? Campo);

    private sealed record ProdutoDeclarado(string AtoCodigo, PapelDaPublicacaoPrevista? Papel, string Lugar, string Produto)
    {
        /// <summary>O campo do papel do produto, onde a recusa do papel do ato mora.</summary>
        internal string Campo => $"{Produto}.papel";

        /// <summary>O campo do tipo de ato do produto, onde a repetição do tipo mora.</summary>
        internal string CampoDoAto => $"{Produto}.atoCodigo";
    }
}
