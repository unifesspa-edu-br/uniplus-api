namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro do calendário de dias úteis congelado no momento da
/// publicação (UNI-REQ-0080, ADR-0061). Declarados como constantes pelo mesmo
/// motivo de <see cref="DiaNaoUtilCongeladoErrorCodes"/>.
/// </summary>
public static class CalendarioDiasUteisCongeladoErrorCodes
{
    public const string OrigemObrigatoria = "CalendarioDiasUteisCongelado.OrigemObrigatoria";
    public const string VersaoDatasetObrigatoria = "CalendarioDiasUteisCongelado.VersaoDatasetObrigatoria";
    public const string VersaoDatasetTamanho = "CalendarioDiasUteisCongelado.VersaoDatasetTamanho";
    public const string DiasObrigatorios = "CalendarioDiasUteisCongelado.DiasObrigatorios";
    public const string SemDiaNaoUtil = "CalendarioDiasUteisCongelado.SemDiaNaoUtil";
    public const string DiaDuplicado = "CalendarioDiasUteisCongelado.DiaDuplicado";
}
