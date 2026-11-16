namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

/// <summary>O código de um fato criado pelo teste, que não colide com o seed nem com outro teste da coleção.</summary>
internal static class FatoCandidatoCodigoDeTeste
{
    internal static string CodigoUnico() => $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
