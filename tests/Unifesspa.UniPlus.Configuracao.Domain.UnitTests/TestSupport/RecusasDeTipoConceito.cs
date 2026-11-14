namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Confere que uma recusa traz o código de erro no campo esperado.</summary>
internal static class RecusasDeTipoConceito
{
    public static void DeveRecusarComo<T>(this Result<T> resultado, string codigoDoErro, string campo)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(
            erro => erro.Error.Code == codigoDoErro && erro.Field == campo,
            $"o erro {codigoDoErro} esperado no campo {campo}; vieram: {string.Join(", ", resultado.Errors.Select(e => $"{e.Field}={e.Error.Code}"))}");
    }
}
