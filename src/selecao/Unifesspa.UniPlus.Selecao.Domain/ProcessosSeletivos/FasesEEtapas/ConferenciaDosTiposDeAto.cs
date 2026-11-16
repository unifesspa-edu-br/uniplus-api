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
    /// atos já registrados já ocupam: o ato da retificação continua a própria linhagem, e por isso conta uma vez só. Uma causa
    /// por tipo, sem campo, porque a repetição envolve mais de um lugar.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirTiposUnicosRepetidos(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        return
        [
            .. DeclaracoesDeTipoUnico(cronograma, etapas, tipos)
                .GroupBy(static declaracao => declaracao.AtoCodigo, StringComparer.Ordinal)
                .Where(static grupo => grupo.Count() > 1)
                .Select(grupo => new FieldError(null, TipoUnicoRepetido(tipos.NomeDe(grupo.Key), grupo.Select(static declaracao => declaracao.Lugar)))),
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

    private static IEnumerable<DeclaracaoDeAto> DeclaracoesDeTipoUnico(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
    [
        .. ProdutosDeclarados(cronograma, etapas)
            .Where(produto => tipos.EhUnico(produto.AtoCodigo))
            .Select(static produto => new DeclaracaoDeAto(produto.AtoCodigo, produto.Lugar, produto.Papel)),
        .. TiposDeAtoDoCronograma.VagasJaReivindicadas(tipos),
    ];

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

    // Os produtos das fases primeiro, depois os das etapas, cada um com o campo do papel que o localiza.
    private static IEnumerable<ProdutoDeclarado> ProdutosDeclarados(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return
        [
            .. cronograma.Fases.SelectMany(static (fase, posicao) => fase.Atos.Produtos.Select(produto => new ProdutoDeclarado(
                produto.AtoCodigo, produto.Papel, LugarDaDeclaracao.DaFase(fase.Codigo), $"fases[{posicao}].produtos[{produto.Posicao}].papel"))),
            .. etapas.Etapas.SelectMany(static (etapa, posicao) => etapa.Atos.Produtos.Select(produto => new ProdutoDeclarado(
                produto.AtoCodigo, produto.Papel, LugarDaDeclaracao.DaEtapa(etapa.Nome, etapa.Ordem), $"etapas[{posicao}].produtos[{produto.Posicao}].papel"))),
        ];
    }

    private static ItemConformidade Item(string codigo, string mensagem, int causas) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, causas == 0);

    private sealed record ProdutoDeclarado(string AtoCodigo, PapelProdutoFase? Papel, string Lugar, string Campo);
}
