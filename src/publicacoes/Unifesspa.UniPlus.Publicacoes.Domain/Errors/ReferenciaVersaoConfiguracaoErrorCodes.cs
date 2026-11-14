namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Códigos de erro do value object <c>ReferenciaVersaoConfiguracao</c>. O
/// agregado traduz a falha para <c>AtoNormativo.VersaoInvocadaIncompleta</c>
/// antes de devolvê-la, mas os códigos ficam registrados para que a propagação
/// direta responda 422 em vez do 500 genérico do mapper.
/// </summary>
public static class ReferenciaVersaoConfiguracaoErrorCodes
{
    public const string IdObrigatorio = "ReferenciaVersaoConfiguracao.IdObrigatorio";
    public const string HashInvalido = "ReferenciaVersaoConfiguracao.HashInvalido";
}
