namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Marker para carregadores de assembly (ArchUnitNET). Readers cross-módulo
/// (IModalidadeReader, INecessidadeEspecialReader, ITipoDocumentoReader,
/// IEnderecoReader) e seus *View DTOs entram em F2.
/// </summary>
public sealed class ConfiguracaoContractsAssemblyMarker
{
    private ConfiguracaoContractsAssemblyMarker()
    {
    }
}
