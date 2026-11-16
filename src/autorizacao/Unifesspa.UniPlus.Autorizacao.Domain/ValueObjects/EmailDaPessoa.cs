namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O e-mail de uma pessoa como o módulo o guarda e o compara.</summary>
public static class EmailDaPessoa
{
    /// <summary>O tamanho máximo do e-mail nas colunas do módulo.</summary>
    public const int MaxLength = 254;

    /// <summary>O e-mail em minúsculas e de formato válido, que cabe na coluna e não traz o caractere nulo; nulo se inválido.</summary>
    public static string? Normalizar(string? email)
    {
        Result<Email> resultado = Email.Criar(email);

        return resultado.IsSuccess && resultado.Value!.Valor.Length <= MaxLength && !resultado.Value.Valor.Contains('\0', StringComparison.Ordinal)
            ? resultado.Value.Valor
            : null;
    }

    /// <summary>O domínio do e-mail é um dos institucionais.</summary>
    public static bool Institucional(string email, IDominiosInstitucionais dominios)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(dominios);

        return dominios.Contem(email[(email.LastIndexOf('@') + 1)..]);
    }
}
