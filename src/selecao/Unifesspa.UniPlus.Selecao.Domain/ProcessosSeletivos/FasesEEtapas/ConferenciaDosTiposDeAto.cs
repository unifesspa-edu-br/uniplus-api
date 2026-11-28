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
        ViolacoesDoProduto(cronograma, etapas, tipos, static (classificacao, produto, nome) =>
            classificacao.ViolacaoDoResultadoSemPapel(produto.Papel, nome));

    public static IReadOnlyList<FieldError> ConferirTiposQueCongelamComoResultado(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
        ViolacoesDoProduto(cronograma, etapas, tipos, static (classificacao, produto, nome) =>
            classificacao.ViolacaoDoTipoQueCongelaComPapel(produto.Papel, nome));

    /// <summary>
    /// O produto que declara um tipo sem versão vigente no catálogo nem cópia na versão publicada: a publicação congela a
    /// classificação de cada tipo declarado, e sem ela não há o que congelar. Uma recusa por produto, no campo do tipo, sem
    /// nome nem código — o tipo não tem nome a citar, e o campo aponta o item.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirTiposSemVersaoVigente(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        return
        [
            .. ProdutosDeclarados(cronograma, etapas)
                .Where(produto => tipos.ClassificacaoDe(produto.AtoCodigo) is null)
                .Select(static produto => new FieldError(produto.CampoDoAto, TipoSemVersaoVigente())),
        ];
    }

    internal static DomainError TipoSemVersaoVigente() => new(
        ProcessoSeletivoErrorCodes.TipoDeAtoSemVersaoVigente,
        "O tipo de ato declarado no item não tem versão vigente no catálogo de Publicações, e a publicação congela a classificação de cada tipo declarado.");

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
                .SelectMany(grupo => CausasDoTipoRepetido(tipos.Nomes.NomeDe(grupo.Key)!, [.. grupo])),
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

    internal static DomainError TipoUnicoRepetido(string nomeDoTipo) => new(
        ProcessoSeletivoErrorCodes.TipoDeAtoUnicoDeclaradoMaisDeUmaVez,
        $"O tipo de ato '{nomeDoTipo}' só pode ser publicado uma vez por processo e está declarado mais de uma vez.");

    // Os produtos primeiro, na posição de entrada, e depois as vagas já ocupadas, que não têm campo no documento.
    private static IEnumerable<OcorrenciaDoTipo> OcorrenciasDeTipoUnico(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
    [
        .. ProdutosDeclarados(cronograma, etapas)
            .Where(produto => tipos.EhUnico(produto.AtoCodigo))
            .Select(static produto => new OcorrenciaDoTipo(produto.AtoCodigo, produto.CampoDoAto)),
        .. TiposDeAtoDoCronograma.VagasJaReivindicadas(tipos)
            .Select(static vaga => new OcorrenciaDoTipo(vaga.AtoCodigo, null)),
    ];

    private static IEnumerable<FieldError> CausasDoTipoRepetido(string nomeDoTipo, List<OcorrenciaDoTipo> ocorrencias)
    {
        DomainError erro = TipoUnicoRepetido(nomeDoTipo);
        List<string> camposRepetidos = [.. ocorrencias.Skip(1).Select(static ocorrencia => ocorrencia.Campo).OfType<string>()];

        return camposRepetidos.Count == 0
            ? [new FieldError(null, erro)]
            : camposRepetidos.Select(campo => new FieldError(campo, erro));
    }

    private static List<FieldError> ViolacoesDoProduto(
        CronogramaDoProcesso cronograma,
        EtapasDoProcesso etapas,
        TiposDeAtoUnicosPorObjeto tipos,
        Func<ClassificacaoDoTipoDeAto, ProdutoDeclarado, string, DomainError?> violacao)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        return
        [
            .. ProdutosDeclarados(cronograma, etapas)
                .Select(produto => (produto, erro: tipos.ClassificacaoDe(produto.AtoCodigo) is { } classificacao ? violacao(classificacao, produto, tipos.Nomes.NomeDe(produto.AtoCodigo)!) : null))
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
                produto.AtoCodigo, produto.Papel, $"fases[{posicao}].produtos[{produto.Posicao}]"))),
            .. etapas.Etapas.SelectMany(static (etapa, posicao) => etapa.Atos.Produtos.Select(produto => new ProdutoDeclarado(
                produto.AtoCodigo, produto.Papel, $"etapas[{posicao}].produtos[{produto.Posicao}]"))),
        ];
    }

    private static ItemConformidade Item(string codigo, string mensagem, int causas) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, causas == 0);

    private sealed record OcorrenciaDoTipo(string AtoCodigo, string? Campo);

    private sealed record ProdutoDeclarado(string AtoCodigo, PapelDaPublicacaoPrevista? Papel, string Produto)
    {
        /// <summary>O campo do papel do produto, onde a recusa do papel do ato mora.</summary>
        internal string Campo => $"{Produto}.papel";

        /// <summary>O campo do tipo de ato do produto, onde a repetição do tipo mora.</summary>
        internal string CampoDoAto => $"{Produto}.atoCodigo";
    }
}
