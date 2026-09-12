namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Um produto que uma <see cref="EtapaProcesso"/> publica (0..*): o código do tipo de ato e
/// o papel daquela publicação no ciclo recursal da etapa.
/// </summary>
/// <remarks>
/// <para>
/// Gêmeo de <see cref="ProdutoDaFase"/> num nível abaixo. Existe porque o certame publica
/// por etapa, não só por fase: a prova objetiva divulga o gabarito e, depois, o resultado
/// da própria prova — dois ciclos independentes dentro da mesma fase de avaliação, que um
/// produto pendurado na fase não distingue.
/// </para>
/// <para>
/// <b>Declara intenção, nunca estado</b>, pelo mesmo motivo do produto da fase: o produto
/// diz o que a etapa publicará, e o ato efetivamente publicado é fato, registrado em
/// Publicações.
/// </para>
/// </remarks>
public sealed class ProdutoDaEtapa : EntityBase
{
    public Guid EtapaProcessoId { get; private set; }

    /// <summary>Código do tipo de ato publicado — chave natural dentro da etapa.</summary>
    public string AtoCodigo { get; private set; } = string.Empty;

    /// <summary>
    /// O papel da publicação no ciclo recursal da etapa, ou <see langword="null"/> quando o
    /// ato não é resultado — a etapa publica um aviso sem por isso produzir resultado.
    /// </summary>
    public PapelProdutoFase? Papel { get; private set; }

    private ProdutoDaEtapa() { }

    /// <summary>
    /// Cria o produto. Que o tipo de ato exista, esteja vigente e seja resultado no catálogo
    /// é I/O — resolvido em Application (ADR-0042).
    /// </summary>
    public static ProdutoDaEtapa Criar(string atoCodigo, PapelProdutoFase? papel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atoCodigo);

        return new ProdutoDaEtapa
        {
            AtoCodigo = atoCodigo.Trim(),
            Papel = papel,
        };
    }

    /// <summary>Reidrata o produto preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static ProdutoDaEtapa Reidratar(Guid id, string atoCodigo, PapelProdutoFase? papel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atoCodigo);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O produto reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }

        return new ProdutoDaEtapa
        {
            Id = id,
            AtoCodigo = atoCodigo.Trim(),
            Papel = papel,
        };
    }

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;
}
