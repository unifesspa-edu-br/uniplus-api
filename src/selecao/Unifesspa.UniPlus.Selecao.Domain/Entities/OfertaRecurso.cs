namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Recurso de acessibilidade ofertado pelo processo (ex.: ledor, prova
/// ampliada), congelado por snapshot-copy (ADR-0061) do cadastro de
/// <c>RecursoAcessibilidade</c> do módulo Configuração.
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete): ver justificativa em
/// <see cref="EtapaProcesso"/>.
/// </remarks>
public sealed class OfertaRecurso : EntityBase
{
    public Guid OfertaAtendimentoEspecializadoId { get; private set; }
    public Guid RecursoOrigemId { get; private set; }
    public string RecursoNome { get; private set; } = string.Empty;

    private OfertaRecurso() { }

    public static OfertaRecurso Criar(Guid recursoOrigemId, string recursoNome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recursoNome);
        if (recursoOrigemId == Guid.Empty)
        {
            throw new ArgumentException("RecursoOrigemId não pode ser Guid vazio.", nameof(recursoOrigemId));
        }

        return new OfertaRecurso
        {
            RecursoOrigemId = recursoOrigemId,
            RecursoNome = recursoNome.Trim(),
        };
    }

    internal void VincularOferta(Guid ofertaAtendimentoEspecializadoId) =>
        OfertaAtendimentoEspecializadoId = ofertaAtendimentoEspecializadoId;
}
