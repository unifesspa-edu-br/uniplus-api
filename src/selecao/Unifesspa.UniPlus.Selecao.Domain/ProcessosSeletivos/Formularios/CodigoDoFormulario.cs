namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O código que identifica o formulário dentro do processo (ADR-0171): uma letra maiúscula, seguida de
/// letras maiúsculas, dígitos e sublinhados isolados, até sessenta caracteres. É único no processo e não muda
/// depois de criado, porque o documento e o envelope congelado citam o formulário por ele. A ordem dos
/// códigos é a ordinal, a mesma em qualquer cultura.
/// </summary>
public sealed record CodigoDoFormulario
{
    public const int TamanhoMaximo = 60;

    /// <summary>A ordem em que os formulários são listados: a dos códigos, comparados caractere a caractere.</summary>
    public static IComparer<CodigoDoFormulario> OrdemOrdinal { get; } =
        Comparer<CodigoDoFormulario>.Create(static (primeiro, segundo) => string.CompareOrdinal(primeiro.Valor, segundo.Valor));

    public string Valor { get; }

    private CodigoDoFormulario(string valor) => Valor = valor;

    /// <summary>Aceita o código no formato fechado, como recebido.</summary>
    public static Result<CodigoDoFormulario> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoDoFormulario>.Failure(new DomainError(
                CodigoDoFormularioErrorCodes.Obrigatorio,
                "O código do formulário é obrigatório."));
        }

        return TemFormatoValido(valor)
            ? Result<CodigoDoFormulario>.Success(new CodigoDoFormulario(valor))
            : Result<CodigoDoFormulario>.Failure(new DomainError(
                CodigoDoFormularioErrorCodes.FormatoInvalido,
                "O código do formulário começa com letra maiúscula e tem só letras maiúsculas, dígitos e "
                + $"sublinhados isolados, até {TamanhoMaximo} caracteres (ex.: INSCRICAO)."));
    }

    public override string ToString() => Valor;

    private static bool TemFormatoValido(string texto) =>
        texto.Length <= TamanhoMaximo
        && FormatoDoCodigo.TemFormatoValido(texto);
}
