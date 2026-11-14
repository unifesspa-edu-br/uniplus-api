namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Códigos de erro do value object <c>ReferenciaVersaoDoObjeto</c>. O
/// agregado traduz a falha para <c>AtoNormativo.VersaoDoObjetoIncompleta</c>
/// antes de devolvê-la, mas os códigos ficam registrados para que a propagação
/// direta responda 422 em vez do 500 genérico do mapper.
/// </summary>
public static class ReferenciaVersaoDoObjetoErrorCodes
{
    public const string IdObrigatorio = "ReferenciaVersaoDoObjeto.IdObrigatorio";
    public const string HashInvalido = "ReferenciaVersaoDoObjeto.HashInvalido";
}
