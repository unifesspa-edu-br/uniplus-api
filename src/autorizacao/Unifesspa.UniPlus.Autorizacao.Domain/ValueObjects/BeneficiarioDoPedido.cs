namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

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

    /// <summary>
    /// A pessoa é o beneficiário: o já conhecido se confere pela identidade do token; o ainda desconhecido, pelo e-mail
    /// verificado, que é o do pedido.
    /// </summary>
    internal bool Reconhece(PessoaNoToken pessoa, string emailDoPedido) =>
        Identidade is not null
            ? Eh(pessoa.Identidade)
            : pessoa.EmailVerificado && EmailDaPessoa.Normalizar(pessoa.Email) is { } email && string.Equals(email, emailDoPedido, StringComparison.Ordinal);

    internal DomainError? RecusaDoAceite(PessoaNoToken pessoa, string emailDoPedido)
    {
        if (!Reconhece(pessoa, emailDoPedido))
        {
            return new DomainError(
                PedidoErrorCodes.NaoEOBeneficiario,
                "Só a pessoa a quem o pedido se destina, com o e-mail institucional verificado, o aceita.");
        }

        // O aprovador julga a pessoa pelo nome: sem ele no token, o pedido não avança à aprovação.
        return NomeDeExibicao.Normalizar(pessoa.Nome) is null ? SemNome() : null;
    }

    internal static BeneficiarioDoPedido AceitoPor(PessoaNoToken pessoa, DateTimeOffset instante) =>
        Aceito(pessoa.Identidade, NomeDeExibicao.Normalizar(pessoa.Nome), instante);

    internal static DomainError SemNome() => new(
        PedidoErrorCodes.BeneficiarioSemNome,
        "A sessão do beneficiário não traz o nome completo, que o aprovador precisa para julgar o pedido.");
}
