namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O documento de designação dos gestores como a instalação o recebe: o número que o identifica e o tamanho declarado do PDF.</summary>
public sealed record DocumentoDeDesignacao
{
    public const int NumeroMaxLength = 60;
    public const long TamanhoMaximoDoPdfBytes = 10L * 1024 * 1024;

    public string Numero { get; }

    /// <summary>Tamanho declarado do PDF, em bytes; é o limite do pedido ao verificador, no máximo <see cref="TamanhoMaximoDoPdfBytes"/>.</summary>
    public long TamanhoDoPdf { get; }

    private DocumentoDeDesignacao(string numero, long tamanhoDoPdf)
    {
        Numero = numero;
        TamanhoDoPdf = tamanhoDoPdf;
    }

    /// <summary>Confere o número e o tamanho, acumulando as duas violações (ADR-0125).</summary>
    internal static Result<DocumentoDeDesignacao> Conferir(string? numero, long tamanhoDoPdf)
    {
        string? normalizado = NormalizarNumero(numero);
        FieldError[] erros = [.. ErroDoNumero(normalizado), .. ErroDoTamanho(tamanhoDoPdf)];

        return erros.Length > 0
            ? Result<DocumentoDeDesignacao>.ValidationFailure(erros)
            : Result<DocumentoDeDesignacao>.Success(new DocumentoDeDesignacao(normalizado!, tamanhoDoPdf));
    }

    // O caractere nulo cabe no tamanho, mas a coluna textual do banco não o armazena.
    private static string? NormalizarNumero(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return null;
        }

        string normalizado = numero.Trim();

        return normalizado.Contains('\0', StringComparison.Ordinal) || normalizado.Length > NumeroMaxLength
            ? null
            : normalizado;
    }

    private static FieldError[] ErroDoNumero(string? normalizado) =>
        normalizado is not null
            ? []
            : [new("numeroDoDocumento", new DomainError(
                InstalacaoErrorCodes.DocumentoInvalido,
                $"O número do documento de designação é obrigatório e deve ter até {NumeroMaxLength} caracteres."))];

    private static FieldError[] ErroDoTamanho(long tamanhoDoPdf) =>
        tamanhoDoPdf is >= 1 and <= TamanhoMaximoDoPdfBytes
            ? []
            : [new("tamanhoDoPdf", new DomainError(
                InstalacaoErrorCodes.DocumentoInvalido,
                "O PDF do documento de designação deve ter de 1 byte a 10 MB."))];
}
