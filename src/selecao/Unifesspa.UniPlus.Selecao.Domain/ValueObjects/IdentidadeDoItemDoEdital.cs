namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using Enums;

/// <summary>
/// O que identifica um item que o edital declara publicar, entre versões de configuração: onde ele está,
/// o tipo de ato e o papel no ciclo recursal.
/// </summary>
/// <remarks>
/// <para>
/// O <c>Id</c> do produto não serve a isso: ele é identidade dentro de uma versão, e muda sem que o item
/// mude quando o descarte de uma sessão de retificação recria os produtos de uma fase, ou quando a sessão
/// remove o item e o declara de novo. O ato publicado, que precisa reconhecer o mesmo item na versão
/// seguinte, guarda esta identidade.
/// </para>
/// <para>
/// O lugar é a fase canônica de origem da fase, única no cronograma, ou o <c>Id</c> da etapa, que a
/// restauração da configuração preserva. O nome da etapa e o código da fase são rótulos editoriais; a
/// ordem muda quando a sessão desloca as fases.
/// </para>
/// </remarks>
public sealed record IdentidadeDoItemDoEdital
{
    public const int ChaveMaxLength = 160;

    private const string SemPapel = "-";

    private IdentidadeDoItemDoEdital(string lugar, string atoCodigo, PapelProdutoFase? papel)
    {
        Lugar = lugar;
        AtoCodigo = atoCodigo;
        Papel = papel;
    }

    public string Lugar { get; }

    public string AtoCodigo { get; }

    public PapelProdutoFase? Papel { get; }

    /// <summary>A forma gravada no ato publicado: o par lugar, tipo de ato e papel, numa só chave.</summary>
    public string Chave => $"{Lugar}|{AtoCodigo}|{Papel.ToCodigo() ?? SemPapel}";

    public static IdentidadeDoItemDoEdital DaFase(FaseCronograma fase, ProdutoDaFase produto)
    {
        ArgumentNullException.ThrowIfNull(fase);
        ArgumentNullException.ThrowIfNull(produto);

        return new($"fase:{fase.FaseCanonicaOrigemId:D}", produto.AtoCodigo, produto.Papel);
    }

    public static IdentidadeDoItemDoEdital DaEtapa(EtapaProcesso etapa, ProdutoDaEtapa produto)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(produto);

        return new($"etapa:{etapa.Id:D}", produto.AtoCodigo, produto.Papel);
    }

    /// <summary>A identidade de cada produto do cronograma, pelo <c>Id</c> que ele tem naquele cronograma.</summary>
    public static Dictionary<Guid, IdentidadeDoItemDoEdital> Dos(
        IEnumerable<FaseCronograma> fases,
        IEnumerable<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return fases.SelectMany(static f => f.Produtos.Select(p => (p.Id, Item: DaFase(f, p))))
            .Concat(etapas.SelectMany(static e => e.Produtos.Select(p => (p.Id, Item: DaEtapa(e, p)))))
            .ToDictionary(static i => i.Id, static i => i.Item);
    }
}
