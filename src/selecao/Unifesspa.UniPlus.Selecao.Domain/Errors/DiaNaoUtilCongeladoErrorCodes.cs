namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro do dia não útil congelado no momento da publicação
/// (UNI-REQ-0080, ADR-0061). Declarados como constantes porque o código é
/// entregue ao <c>DomainError</c> por um helper: escrito como literal na
/// chamada, ele deixaria de ser visível para o gate de cobertura do registro
/// de erros, e o mapeamento faltante só apareceria como <c>500</c> na
/// requisição que o disparasse.
/// </summary>
public static class DiaNaoUtilCongeladoErrorCodes
{
    public const string DataAusente = "DiaNaoUtilCongelado.DataAusente";
    public const string AbrangenciaInvalida = "DiaNaoUtilCongelado.AbrangenciaInvalida";
    public const string MunicipioEmDiaEstadual = "DiaNaoUtilCongelado.MunicipioEmDiaEstadual";
    public const string UfAusenteEmDiaEstadual = "DiaNaoUtilCongelado.UfAusenteEmDiaEstadual";
    public const string UfInvalida = "DiaNaoUtilCongelado.UfInvalida";
    public const string TerritorioEmDiaSemRecorte = "DiaNaoUtilCongelado.TerritorioEmDiaSemRecorte";
}
