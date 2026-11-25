namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia;

internal static class DadosDeTipoDeficiencia
{
    internal static string CodigoUnico() => $"DEF_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    internal static string NomeDePersistenciaUnico() => CodigoUnico();
}
