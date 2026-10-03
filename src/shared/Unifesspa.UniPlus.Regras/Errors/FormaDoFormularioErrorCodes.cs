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
    public const string OpcionalQueAlimentaRegra = "ItemFormulario.OpcionalQueAlimentaRegra";
    public const string TipoRenderizacaoIncoerenteComDominio = "ItemFormulario.TipoRenderizacaoIncoerenteComDominio";
    public const string FatoDesconhecido = "ItemFormulario.FatoDesconhecido";
    public const string FatoNaoColetavel = "ItemFormulario.FatoNaoColetavel";
    public const string ItensEmExcesso = "ItemFormulario.ItensEmExcesso";
    public const string OpcoesDeOutroDominio = "ItemFormulario.OpcoesDeOutroDominio";
    public const string MunicipioSemUf = "ItemFormulario.MunicipioSemUf";
    public const string UfDoMunicipioInvalida = "ItemFormulario.UfDoMunicipioInvalida";
}

/// <summary>Códigos de recusa da forma de um grupo repetível do formulário, no processo e no modelo.</summary>
public static class GrupoFormularioErrorCodes
{
    public const string CodigoInvalido = "GrupoFormulario.CodigoInvalido";
    public const string OrdemInvalida = "GrupoFormulario.OrdemInvalida";
    public const string RotuloInvalido = "GrupoFormulario.RotuloInvalido";
    public const string ContagemIncoerente = "GrupoFormulario.ContagemIncoerente";
    public const string SubitensForaDoLimite = "GrupoFormulario.SubitensForaDoLimite";
    public const string RegraAutorreferente = "GrupoFormulario.RegraAutorreferente";
    public const string CampoComSecaoPropria = "GrupoFormulario.CampoComSecaoPropria";
    public const string CandidatoComoMembroIncompleto = "GrupoFormulario.CandidatoComoMembroIncompleto";
}

/// <summary>Códigos de recusa de vínculo novo a fato ou valor desativado no catálogo, no processo e no modelo.</summary>
public static class VinculoCatalogoErrorCodes
{
    public const string FatoDesativado = "VinculoCatalogo.FatoDesativado";
    public const string ValorDesativado = "VinculoCatalogo.ValorDesativado";
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
