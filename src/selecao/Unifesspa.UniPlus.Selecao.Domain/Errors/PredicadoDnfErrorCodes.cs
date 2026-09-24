namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro do predicado sobre fatos do candidato que quem o valida precisa distinguir
/// para apontar o campo da recusa: operador ou valor.
/// </summary>
public static class PredicadoDnfErrorCodes
{
    public const string OperadorIncompativelComDominio = "PredicadoDnf.OperadorIncompativelComDominio";
    public const string ValorIncompativelComTipo = "PredicadoDnf.ValorIncompativelComTipo";
    public const string ValorForaDoDominio = "PredicadoDnf.ValorForaDoDominio";
}
