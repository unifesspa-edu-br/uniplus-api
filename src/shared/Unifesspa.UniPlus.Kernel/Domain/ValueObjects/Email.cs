namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Results;

public sealed partial record Email
{
    public string Valor { get; }

    private Email(string valor) => Valor = valor;

    public static Result<Email> Criar(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result<Email>.Failure(new DomainError("Email.Vazio", "E-mail é obrigatório."));

        // E-mail é case-insensitive por RFC 5321, normalização para lowercase é intencional
#pragma warning disable CA1308
        string normalizado = email.Trim().ToLowerInvariant();
#pragma warning restore CA1308

        if (!EmailRegex().IsMatch(normalizado))
            return Result<Email>.Failure(new DomainError("Email.Invalido", "E-mail inválido."));

        return Result<Email>.Success(new Email(normalizado));
    }

    public override string ToString() => Valor;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
