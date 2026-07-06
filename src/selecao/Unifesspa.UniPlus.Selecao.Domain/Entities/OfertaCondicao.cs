namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Condição de atendimento especializado ofertada pelo processo (ex.: PCD,
/// lactante, sabatista), congelada por snapshot-copy (ADR-0061) do cadastro
/// de <c>CondicaoAtendimentoEspecializado</c> do módulo Configuração.
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete): ver justificativa em
/// <see cref="EtapaProcesso"/>.
/// </remarks>
public sealed class OfertaCondicao : EntityBase
{
    public Guid OfertaAtendimentoEspecializadoId { get; private set; }
    public Guid CondicaoOrigemId { get; private set; }
    public string CondicaoCodigo { get; private set; } = string.Empty;
    public string CondicaoNome { get; private set; } = string.Empty;

    private OfertaCondicao() { }

    public static OfertaCondicao Criar(Guid condicaoOrigemId, string condicaoCodigo, string condicaoNome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(condicaoCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(condicaoNome);
        if (condicaoOrigemId == Guid.Empty)
        {
            throw new ArgumentException("CondicaoOrigemId não pode ser Guid vazio.", nameof(condicaoOrigemId));
        }

        return new OfertaCondicao
        {
            CondicaoOrigemId = condicaoOrigemId,
            CondicaoCodigo = condicaoCodigo.Trim(),
            CondicaoNome = condicaoNome.Trim(),
        };
    }

    internal void VincularOferta(Guid ofertaAtendimentoEspecializadoId) =>
        OfertaAtendimentoEspecializadoId = ofertaAtendimentoEspecializadoId;
}
