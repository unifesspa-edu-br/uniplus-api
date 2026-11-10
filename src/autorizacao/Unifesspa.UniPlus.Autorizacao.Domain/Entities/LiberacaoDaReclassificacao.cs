namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// A decisão de uma etapa sobre a permissão reclassificada numa concessão. Só se acrescenta: é evidência forense
/// (ADR-0063). Guarda quem decidiu, se tinha concessão do perfil, o lote da decisão e o resultado; o motivo da suspensão
/// mantida nunca vai a aviso.
/// </summary>
public sealed class LiberacaoDaReclassificacao : IForensicEntity, IRegistroDaTrilhaDeContencao
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ReclassificacaoId { get; private init; }

    public Guid PerfilId { get; private init; }

    public Guid ConcessaoId { get; private init; }

    public EtapaDaVersao Etapa { get; private init; }

    public ResultadoDaLiberacao Resultado { get; private init; }

    /// <summary>Obrigatório quando a etapa mantém a suspensão; nulo na liberação.</summary>
    public string? Motivo { get; private init; }

    public string DecisorEmissor { get; private set; } = string.Empty;

    public string DecisorSub { get; private set; } = string.Empty;

    public string? DecisorNome { get; private set; }

    public bool DecisorTemConcessao { get; private set; }

    public Guid LoteId { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNaTrilha { get; private set; }

    public Identidade Decisor => new(DecisorEmissor, DecisorSub, DecisorNome);

    // Construtor de materialização do EF Core.
    private LiberacaoDaReclassificacao()
    {
    }

    internal static LiberacaoDaReclassificacao Registrar(
        ReclassificacaoDaPermissao reclassificacao, Guid concessaoId, PedidoDeDecisaoDaReclassificacao pedido, int ordinal) =>
        new LiberacaoDaReclassificacao
        {
            ReclassificacaoId = reclassificacao.Id,
            PerfilId = reclassificacao.PerfilId,
            ConcessaoId = concessaoId,
            Etapa = pedido.Etapa,
            Resultado = pedido.Resultado,
            Motivo = pedido.Resultado == ResultadoDaLiberacao.Mantida ? pedido.Motivo : null,
            LoteId = pedido.LoteId,
            Instante = pedido.Instante,
            OrdinalLocal = ordinal,
        }.PorDecisor(pedido);

    private LiberacaoDaReclassificacao PorDecisor(PedidoDeDecisaoDaReclassificacao pedido)
    {
        DecisorEmissor = pedido.Decisor.Emissor;
        DecisorSub = pedido.Decisor.Sub;
        DecisorNome = pedido.Decisor.Nome;
        DecisorTemConcessao = pedido.DecisorTemConcessao;

        return this;
    }
}
