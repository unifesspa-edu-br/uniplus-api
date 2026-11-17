namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Códigos de erro do value object <c>ReferenciaVersaoDoObjeto</c>. A montagem do ato
/// traduz a falha para <c>AtoNormativo.VersaoDoObjetoIncompleta</c>; estes códigos não
/// chegam a uma resposta e por isso não têm mapeamento HTTP.
/// </summary>
public static class ReferenciaVersaoDoObjetoErrorCodes
{
    public const string IdObrigatorio = "ReferenciaVersaoDoObjeto.IdObrigatorio";
    public const string HashInvalido = "ReferenciaVersaoDoObjeto.HashInvalido";
}
