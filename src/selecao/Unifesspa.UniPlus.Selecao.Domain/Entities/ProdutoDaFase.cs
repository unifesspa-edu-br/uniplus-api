namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Um produto que uma <see cref="FaseCronograma"/> publica (0..*): o código do tipo de ato
/// e o papel que aquela publicação tem no ciclo recursal da fase.
/// </summary>
/// <remarks>
/// <para>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="BancaRequerida"/>: a configuração em rascunho é substituível por inteiro
/// (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>).
/// </para>
/// <para>
/// <b>Declara intenção, nunca estado.</b> O produto diz o que a fase publicará; não guarda
/// se já foi publicado, quando, nem por quem — isso é fato, e vive no ato registrado em
/// Publicações. Estado mutável dentro do snapshot congelado faria a mesma
/// <c>VersaoConfiguracao</c> significar coisas diferentes em momentos diferentes, que é o
/// que o regime append-only existe para impedir.
/// </para>
/// <para>
/// <b>O <see cref="EntityBase.Id"/> é congelado no envelope e preservado na reidratação</b>
/// (<see cref="Reidratar"/>), pelo mesmo motivo de <see cref="FaseCronograma.Reidratar"/>:
/// é contra a versão imutável que o ato publicado resolverá, de volta, a configuração de
/// recurso que lhe corresponde.
/// </para>
/// </remarks>
public sealed class ProdutoDaFase : EntityBase
{
    public Guid FaseCronogramaId { get; private set; }

    /// <summary>Código do tipo de ato publicado (ex.: <c>"RESULTADO_PRELIMINAR"</c>) — chave natural dentro da fase.</summary>
    public string AtoCodigo { get; private set; } = string.Empty;

    /// <summary>
    /// O papel da publicação no ciclo recursal da fase, ou <see langword="null"/> quando o
    /// ato não é resultado — a fase publica um aviso sem que isso a torne produtora de
    /// resultado.
    /// </summary>
    public PapelProdutoFase? Papel { get; private set; }

    private ProdutoDaFase() { }

    /// <summary>
    /// Cria o produto. Que o tipo de ato exista, esteja vigente e seja resultado no
    /// catálogo é I/O — resolvido em Application (ADR-0042), onde a view do tipo já está em
    /// mãos.
    /// </summary>
    public static ProdutoDaFase Criar(string atoCodigo, PapelProdutoFase? papel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atoCodigo);

        return new ProdutoDaFase
        {
            AtoCodigo = atoCodigo.Trim(),
            Papel = papel,
        };
    }

    /// <summary>
    /// Reidrata o produto a partir de uma <c>VersaoConfiguracao</c> congelada, preservando
    /// o <see cref="EntityBase.Id"/> — ver o <c>&lt;remarks&gt;</c> da classe.
    /// </summary>
    public static ProdutoDaFase Reidratar(Guid id, string atoCodigo, PapelProdutoFase? papel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atoCodigo);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O produto reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }

        return new ProdutoDaFase
        {
            Id = id,
            AtoCodigo = atoCodigo.Trim(),
            Papel = papel,
        };
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;

    /// <summary>
    /// Retarget do papel na instância <b>rastreada</b>, usado pela reconciliação por
    /// <see cref="AtoCodigo"/> de <see cref="FaseCronograma.AtualizarSnapshot"/> — ver o
    /// <c>&lt;remarks&gt;</c> de lá sobre por que o índice único proíbe recriar a linha.
    /// </summary>
    internal void AtualizarPapel(PapelProdutoFase? papel) => Papel = papel;
}
