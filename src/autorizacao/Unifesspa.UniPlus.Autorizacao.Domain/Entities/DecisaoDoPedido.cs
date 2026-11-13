namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Decisão de um aprovador numa etapa do pedido. Só se acrescenta, nunca se altera: é evidência
/// forense (ADR-0063), sem soft delete nem auditoria de alteração. Guarda o retrato do que foi
/// avaliado (as permissões do perfil e a sensibilidade naquele instante), não uma cópia viva do perfil.
/// </summary>
public sealed class DecisaoDoPedido : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid PedidoId { get; private init; }

    public EtapaDoPedido Etapa { get; private init; }

    public string DecisorEmissor { get; private init; } = string.Empty;

    public string DecisorSub { get; private init; } = string.Empty;

    public string? DecisorNome { get; private init; }

    public ResultadoDaDecisao Resultado { get; private init; }

    /// <summary>Obrigatório na recusa.</summary>
    public string? Motivo { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    /// <summary>
    /// A ordem em que as decisões foram registradas, dada pelo banco: desempata duas decisões do mesmo instante, que a
    /// concorrência otimista do pedido serializa. Zero enquanto a decisão não foi gravada.
    /// </summary>
    public long OrdemDeRegistro { get; private set; }

    /// <summary>Códigos das permissões do perfil no instante da decisão, ordenados.</summary>
    public string[] PermissoesAvaliadas { get; private init; } = [];

    public string SensibilidadeAvaliada { get; private init; } = string.Empty;

    /// <summary>Motivo da dispensa da segunda aprovação, quando a decisão a dispensou (<see cref="MotivosDeDispensa"/>).</summary>
    public string? Dispensa { get; private init; }

    public Identidade Decisor => new(DecisorEmissor, DecisorSub, DecisorNome);

    // Construtor de materialização do EF Core.
    private DecisaoDoPedido()
    {
    }

    internal static DecisaoDoPedido Registrar(
        Guid pedidoId,
        EtapaDoPedido etapa,
        AtoDoAgente ato,
        ResultadoDaDecisao resultado,
        string? motivo,
        PerfilAvaliado perfil,
        string? dispensa)
    {
        return new DecisaoDoPedido
        {
            PedidoId = pedidoId,
            Etapa = etapa,
            DecisorEmissor = ato.Agente.Emissor,
            DecisorSub = ato.Agente.Sub,
            DecisorNome = ato.Agente.Nome,
            Resultado = resultado,
            Motivo = motivo,
            Instante = ato.Instante,
            PermissoesAvaliadas = [.. perfil.Permissoes],
            SensibilidadeAvaliada = perfil.Sensibilidade,
            Dispensa = dispensa,
        };
    }
}
