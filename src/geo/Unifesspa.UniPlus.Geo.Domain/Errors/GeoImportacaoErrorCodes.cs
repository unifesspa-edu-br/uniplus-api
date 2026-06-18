namespace Unifesspa.UniPlus.Geo.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do registro de execução do ETL DNE (Story #674).
/// Seguem o padrão <c>{Entidade}.{Falha}</c> dos demais módulos; o mapeamento para
/// status HTTP / wire code vive em <c>Geo.API/Errors/GeoDomainErrorRegistration</c>.
/// </summary>
public static class GeoImportacaoErrorCodes
{
    /// <summary>Versão (AAAAMM) do dataset não informada.</summary>
    public const string VersaoObrigatoria = "GeoImportacao.VersaoObrigatoria";

    /// <summary>Versão fora do formato AAAAMM (6 dígitos, mês 01–12).</summary>
    public const string VersaoFormatoInvalido = "GeoImportacao.VersaoFormatoInvalido";

    /// <summary>Identificação de quem disparou a carga ausente.</summary>
    public const string DisparadoPorObrigatorio = "GeoImportacao.DisparadoPorObrigatorio";

    /// <summary>Já existe uma carga em andamento — uma execução por vez (concorrência → 409).</summary>
    public const string ImportacaoEmAndamento = "GeoImportacao.ImportacaoEmAndamento";

    /// <summary>A carga foi registrada mas não pôde ser enfileirada (serviço em desligamento → 503).</summary>
    public const string NaoEnfileirada = "GeoImportacao.NaoEnfileirada";

    /// <summary>Transição de estado inválida (ex.: concluir uma execução que não está em andamento).</summary>
    public const string TransicaoInvalida = "GeoImportacao.TransicaoInvalida";
}
