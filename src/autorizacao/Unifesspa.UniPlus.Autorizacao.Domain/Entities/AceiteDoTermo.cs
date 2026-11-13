namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// O aceite do termo de responsabilidade pelo beneficiário do pedido: quem aceitou, quando, e a versão e o resumo do texto
/// apresentado, copiados para que a evidência se leia sem consultar a versão. Só se acrescenta, nunca se altera: é
/// evidência forense (ADR-0063). É assinatura eletrônica simples, autenticada pelo login institucional (ADR-0162), e não
/// grava dado de rede.
/// </summary>
public sealed class AceiteDoTermo : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid PedidoId { get; private init; }

    public Guid VersaoDoTermoId { get; private init; }

    public int Numero { get; private init; }

    public string Resumo { get; private init; } = string.Empty;

    public string PessoaEmissor { get; private init; } = string.Empty;

    public string PessoaSub { get; private init; } = string.Empty;

    public string? PessoaNome { get; private init; }

    public DateTimeOffset AceitoEm { get; private init; }

    public Identidade Pessoa => new(PessoaEmissor, PessoaSub, PessoaNome);

    // Construtor de materialização do EF Core.
    private AceiteDoTermo()
    {
    }

    internal static AceiteDoTermo Registrar(Guid pedidoId, TermoFixado termo, Identidade pessoa, DateTimeOffset instante) =>
        new()
        {
            PedidoId = pedidoId,
            VersaoDoTermoId = termo.VersaoId,
            Numero = termo.Numero,
            Resumo = termo.Resumo,
            PessoaEmissor = pessoa.Emissor,
            PessoaSub = pessoa.Sub,
            PessoaNome = pessoa.Nome,
            AceitoEm = instante,
        };
}
