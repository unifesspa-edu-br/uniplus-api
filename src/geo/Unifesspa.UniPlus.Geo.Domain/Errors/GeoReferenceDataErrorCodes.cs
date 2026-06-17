namespace Unifesspa.UniPlus.Geo.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio das entidades de localidade (reference data DNE+IBGE)
/// do módulo Geo. A validação das factories <c>Importar</c> é mínima — chave
/// natural, nome e proveniência (<c>versao_dataset</c>) não vazios (ADR-0092): o
/// parse tolerante de métricas mora no ETL (F3), não no domínio. Os códigos seguem
/// o padrão <c>{Entidade}.{Falha}</c> usado nos demais módulos.
/// </summary>
public static class GeoReferenceDataErrorCodes
{
    // País
    public const string PaisSiglaIsoObrigatoria = "Pais.SiglaIsoObrigatoria";
    public const string PaisNomeObrigatorio = "Pais.NomeObrigatorio";
    public const string PaisVersaoDatasetObrigatoria = "Pais.VersaoDatasetObrigatoria";

    // Estado
    public const string EstadoPaisObrigatorio = "Estado.PaisObrigatorio";
    public const string EstadoUfObrigatoria = "Estado.UfObrigatoria";
    public const string EstadoNomeObrigatorio = "Estado.NomeObrigatorio";
    public const string EstadoVersaoDatasetObrigatoria = "Estado.VersaoDatasetObrigatoria";

    // Indicador de Estado (satélite 1:1)
    public const string EstadoIndicadorEstadoObrigatorio = "EstadoIndicador.EstadoObrigatorio";
    public const string EstadoIndicadorVersaoDatasetObrigatoria = "EstadoIndicador.VersaoDatasetObrigatoria";

    // Faixa de CEP de Estado
    public const string EstadoFaixaCepEstadoObrigatorio = "EstadoFaixaCep.EstadoObrigatorio";
    public const string EstadoFaixaCepInicialObrigatorio = "EstadoFaixaCep.CepInicialObrigatorio";
    public const string EstadoFaixaCepFinalObrigatorio = "EstadoFaixaCep.CepFinalObrigatorio";
    public const string EstadoFaixaCepVersaoDatasetObrigatoria = "EstadoFaixaCep.VersaoDatasetObrigatoria";
}
