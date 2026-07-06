namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Tipo de deficiência ofertado pelo processo, congelado por snapshot-copy
/// (ADR-0061) do cadastro de <c>TipoDeficiencia</c> do módulo Configuração.
/// Só pode existir quando a condição PcD está ofertada (ADR-0067) — invariante
/// garantida em <see cref="OfertaAtendimentoEspecializado.Criar"/>.
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete): ver justificativa em
/// <see cref="EtapaProcesso"/>.
/// </remarks>
public sealed class OfertaTipoDeficiencia : EntityBase
{
    public Guid OfertaAtendimentoEspecializadoId { get; private set; }
    public Guid TipoDeficienciaOrigemId { get; private set; }
    public string TipoDeficienciaNome { get; private set; } = string.Empty;

    private OfertaTipoDeficiencia() { }

    public static OfertaTipoDeficiencia Criar(Guid tipoDeficienciaOrigemId, string tipoDeficienciaNome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDeficienciaNome);
        if (tipoDeficienciaOrigemId == Guid.Empty)
        {
            throw new ArgumentException("TipoDeficienciaOrigemId não pode ser Guid vazio.", nameof(tipoDeficienciaOrigemId));
        }

        return new OfertaTipoDeficiencia
        {
            TipoDeficienciaOrigemId = tipoDeficienciaOrigemId,
            TipoDeficienciaNome = tipoDeficienciaNome.Trim(),
        };
    }

    internal void VincularOferta(Guid ofertaAtendimentoEspecializadoId) =>
        OfertaAtendimentoEspecializadoId = ofertaAtendimentoEspecializadoId;
}
