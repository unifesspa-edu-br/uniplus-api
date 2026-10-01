namespace Unifesspa.UniPlus.Regras.Errors;

/// <summary>Códigos de recusa da forma de um item do formulário, no processo e no modelo.</summary>
public static class ItemFormularioErrorCodes
{
    public const string FatoCodigoObrigatorio = "ItemFormulario.FatoCodigoObrigatorio";
    public const string FatoCodigoTamanho = "ItemFormulario.FatoCodigoTamanho";
    public const string OrdemInvalida = "ItemFormulario.OrdemInvalida";
    public const string RotuloObrigatorio = "ItemFormulario.RotuloObrigatorio";
    public const string RotuloTamanho = "ItemFormulario.RotuloTamanho";
    public const string TipoRenderizacaoObrigatorio = "ItemFormulario.TipoRenderizacaoObrigatorio";
    public const string AjudaTamanho = "ItemFormulario.AjudaTamanho";
    public const string FormatoIncoerente = "ItemFormulario.FormatoIncoerente";
    public const string RegraAutorreferente = "ItemFormulario.RegraAutorreferente";
    public const string RestricaoIncoerente = "ItemFormulario.RestricaoIncoerente";
}

/// <summary>Códigos de recusa da forma de um termo exigido pelo formulário, no processo e no modelo.</summary>
public static class TermoFormularioErrorCodes
{
    public const string CodigoObrigatorio = "TermoFormulario.CodigoObrigatorio";
    public const string CodigoTamanho = "TermoFormulario.CodigoTamanho";
    public const string OrdemInvalida = "TermoFormulario.OrdemInvalida";
    public const string CodigoDuplicado = "TermoFormulario.CodigoDuplicado";
    public const string OrdemDuplicada = "TermoFormulario.OrdemDuplicada";
}
