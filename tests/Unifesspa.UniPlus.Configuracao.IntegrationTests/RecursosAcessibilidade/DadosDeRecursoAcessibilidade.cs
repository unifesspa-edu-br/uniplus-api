namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.RecursosAcessibilidade;

internal static class DadosDeRecursoAcessibilidade
{
    internal static string CodigoUnico() => $"REC_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    internal static string NomeUnico() => $"Recurso {Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
