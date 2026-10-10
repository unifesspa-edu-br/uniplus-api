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

    /// <summary>
    /// Código do recurso no cadastro, congelado junto com a origem e o nome. É ele que o
    /// candidato escolhe no fato <c>RECURSO_ACESSIBILIDADE</c> e que uma condição cita: o
    /// nome é rótulo editorial e muda sem aviso.
    /// </summary>
    public string RecursoCodigo { get; private set; } = string.Empty;

    public string RecursoNome { get; private set; } = string.Empty;

    private OfertaRecurso() { }

    public static OfertaRecurso Criar(Guid recursoOrigemId, string recursoCodigo, string recursoNome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recursoCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(recursoNome);
        if (recursoOrigemId == Guid.Empty)
        {
            throw new ArgumentException("RecursoOrigemId não pode ser Guid vazio.", nameof(recursoOrigemId));
        }

        return new OfertaRecurso
        {
            RecursoOrigemId = recursoOrigemId,
            RecursoCodigo = recursoCodigo.Trim(),
            RecursoNome = recursoNome.Trim(),
        };
    }

    internal void VincularOferta(Guid ofertaAtendimentoEspecializadoId) =>
        OfertaAtendimentoEspecializadoId = ofertaAtendimentoEspecializadoId;
}
