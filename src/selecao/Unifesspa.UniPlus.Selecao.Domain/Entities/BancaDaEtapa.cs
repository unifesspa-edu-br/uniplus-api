namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma banca que a <see cref="EtapaProcesso"/> requer (0..*) — snapshot-copy do
/// <c>TipoBanca</c> do cadastro, no mesmo desenho de <see cref="BancaRequerida"/> um nível
/// acima.
/// </summary>
/// <remarks>
/// Existe porque quem julga é declarado por etapa, não pela fase inteira: na habilitação, a
/// banca de heteroidentificação julga a etapa de cota racial e a banca biopsicossocial
/// julga a de laudo — duas bancas distintas dentro da mesma fase, que um vínculo no nível
/// da fase não separa.
/// </remarks>
public sealed class BancaDaEtapa : EntityBase
{
    public Guid EtapaProcessoId { get; private set; }

    /// <summary>Id do <c>TipoBanca</c> vivo de origem, no momento do congelamento.</summary>
    public Guid TipoBancaOrigemId { get; private set; }

    /// <summary>Código classificatório congelado (ex.: <c>"BANCA_HETEROIDENTIFICACAO"</c>).</summary>
    public string Codigo { get; private set; } = string.Empty;

    private BancaDaEtapa() { }

    public static BancaDaEtapa Criar(Guid tipoBancaOrigemId, string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        if (tipoBancaOrigemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem do tipo de banca é obrigatório.", nameof(tipoBancaOrigemId));
        }

        return new BancaDaEtapa { TipoBancaOrigemId = tipoBancaOrigemId, Codigo = codigo.Trim() };
    }

    /// <summary>Reidrata a banca preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static BancaDaEtapa Reidratar(Guid id, Guid tipoBancaOrigemId, string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A banca reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        return new BancaDaEtapa { Id = id, TipoBancaOrigemId = tipoBancaOrigemId, Codigo = codigo.Trim() };
    }

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;
}
