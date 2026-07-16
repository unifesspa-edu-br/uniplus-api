namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma banca requerida por uma <see cref="FaseCronograma"/> (0..*, Story #851 §4) —
/// snapshot-copy (ADR-0061) de um <c>TipoBanca</c> do módulo Configuração no momento em
/// que foi vinculada.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>: a configuração em rascunho é substituível por inteiro
/// (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>).
/// </remarks>
public sealed class BancaRequerida : EntityBase
{
    public Guid FaseCronogramaId { get; private set; }

    /// <summary>Id (Guid v7) do <c>TipoBanca</c> vivo de origem, no momento do congelamento.</summary>
    public Guid TipoBancaOrigemId { get; private set; }

    /// <summary>Código classificatório congelado (ex.: <c>"BANCA_ANALISE_DOCUMENTAL"</c>).</summary>
    public string Codigo { get; private set; } = string.Empty;

    private BancaRequerida() { }

    public static BancaRequerida Criar(Guid tipoBancaOrigemId, string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        if (tipoBancaOrigemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem do tipo de banca é obrigatório.", nameof(tipoBancaOrigemId));
        }

        return new BancaRequerida
        {
            TipoBancaOrigemId = tipoBancaOrigemId,
            Codigo = codigo.Trim(),
        };
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;
}
