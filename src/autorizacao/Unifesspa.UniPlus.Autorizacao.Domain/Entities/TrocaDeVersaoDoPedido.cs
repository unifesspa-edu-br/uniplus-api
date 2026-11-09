namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Registro de que uma versão nova do perfil entrou em vigor enquanto o pedido estava ativo, e do que isso fez a ele. Só se
/// acrescenta, nunca se altera, e não tem decisor: quem age é o sistema, que a ADR-0157 separa do ator humano. Guarda os
/// números das versões, não uma cópia do perfil.
/// </summary>
public sealed class TrocaDeVersaoDoPedido : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid PedidoId { get; private init; }

    /// <summary>Número da versão que deixou de ser a em vigor.</summary>
    public int VersaoAnterior { get; private init; }

    /// <summary>Número da versão que passou a ser a em vigor.</summary>
    public int VersaoNova { get; private init; }

    public DesfechoDaTrocaDeVersao Desfecho { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    // Construtor de materialização do EF Core.
    private TrocaDeVersaoDoPedido()
    {
    }

    internal static TrocaDeVersaoDoPedido Registrar(
        Guid pedidoId, int versaoAnterior, int versaoNova, DesfechoDaTrocaDeVersao desfecho, DateTimeOffset instante) =>
        new()
        {
            PedidoId = pedidoId,
            VersaoAnterior = versaoAnterior,
            VersaoNova = versaoNova,
            Desfecho = desfecho,
            Instante = instante,
        };
}
