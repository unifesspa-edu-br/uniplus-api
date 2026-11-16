namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A identidade de quem vai receber o acesso, como o token dele a trouxe. É desconhecida até ele aceitar o pedido, e só então
/// o pedido grava emissor, <c>contaId</c> e nome. O pedido para si mesmo já nasce com ela. Nunca carrega CPF.
/// </summary>
public sealed record BeneficiarioDoPedido
{
    public string? Emissor { get; private init; }

    public string? ContaId { get; private init; }

    public string? Nome { get; private init; }

    /// <summary>Instante do aceite do beneficiário; nulo antes dele e no pedido para si mesmo que não passa pelo aceite.</summary>
    public DateTimeOffset? AceitouEm { get; private init; }

    /// <summary>A identidade do beneficiário; nula enquanto ele não aceitou.</summary>
    public Identidade? Identidade => Emissor is { } emissor && ContaId is { } contaId ? new Identidade(emissor, contaId, Nome) : null;

    // Construtor de materialização do EF Core.
    private BeneficiarioDoPedido()
    {
    }

    /// <summary>O beneficiário que ainda não se identificou: o pedido o encontra pelo e-mail até ele aceitar.</summary>
    public static BeneficiarioDoPedido Desconhecido() => new();

    /// <summary>O beneficiário já identificado, sem aceite: o próprio solicitante num pedido para si mesmo.</summary>
    public static BeneficiarioDoPedido Conhecido(Identidade identidade, string? nome)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        return new BeneficiarioDoPedido { Emissor = identidade.Emissor, ContaId = identidade.ContaId, Nome = nome };
    }

    /// <summary>O beneficiário depois de aceitar o pedido, com a identidade e o nome do token dele.</summary>
    public static BeneficiarioDoPedido Aceito(Identidade identidade, string? nome, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        return Conhecido(identidade, nome) with { AceitouEm = instante };
    }

    public bool Eh(Identidade identidade) => Identidade is { } conhecida && conhecida == identidade;
}
