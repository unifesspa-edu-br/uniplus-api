namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Acesso a um anexo do pedido: quem pediu a URL de leitura e quando. Só se acrescenta, nunca se
/// altera: é evidência forense (ADR-0063) de quem viu o documento.
/// </summary>
public sealed class LeituraDoAnexoDoPedido : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid PedidoId { get; private init; }

    public Guid AnexoId { get; private init; }

    public string LeitorEmissor { get; private init; } = string.Empty;

    public string LeitorSub { get; private init; } = string.Empty;

    public string? LeitorNome { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    // Construtor de materialização do EF Core.
    private LeituraDoAnexoDoPedido()
    {
    }

    public static LeituraDoAnexoDoPedido Registrar(Guid pedidoId, Guid anexoId, Identidade leitor, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(leitor);

        return new LeituraDoAnexoDoPedido
        {
            PedidoId = pedidoId,
            AnexoId = anexoId,
            LeitorEmissor = leitor.Emissor,
            LeitorSub = leitor.Sub,
            LeitorNome = leitor.Nome,
            Instante = instante,
        };
    }
}
