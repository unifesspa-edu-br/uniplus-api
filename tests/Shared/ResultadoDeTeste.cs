namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Obtém o valor de um <see cref="Result{T}"/> que a montagem do cenário de teste espera bem-sucedido; se a montagem
/// falhar, o teste para com a mensagem do domínio em vez de seguir com um valor ausente.
/// </summary>
internal static class ResultadoDeTeste
{
    public static T ValorEsperado<T>(this Result<T> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.Value is { } valor
            ? valor
            : throw new InvalidOperationException($"Cenário de teste inválido: {resultado.Error?.Message}");
    }
}
