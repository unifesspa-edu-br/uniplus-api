namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Reduz as recusas ao que o teste confere: o campo e o código, na ordem recebida.</summary>
internal static class ResumoDasRecusas
{
    public static IEnumerable<(string? Campo, string Codigo)> De(IEnumerable<FieldError> recusas) =>
        recusas.Select(recusa => (recusa.Field, recusa.Error.Code));
}
