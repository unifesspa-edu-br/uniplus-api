namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Valores de TipoAbrangencia.</summary>
public enum TipoAbrangencia
{
    [CodigoDeContrato("FEDERAL", "Federal", Descricao = "Aplica-se a norma federal.")]
    Federal = 1,
    [CodigoDeContrato("ESTADUAL", "Estadual", Descricao = "Aplica-se a norma estadual.")]
    Estadual = 2,
    [CodigoDeContrato("MUNICIPAL", "Municipal", Descricao = "Aplica-se a norma municipal.")]
    Municipal = 3,
    [CodigoDeContrato("INTERNA_NORMA", "Norma interna", Descricao = "Aplica-se a norma da instituição.")]
    InternaNorma = 4,
    [CodigoDeContrato("INTERNA_EDITAL", "Edital", Descricao = "Aplica-se o próprio edital.")]
    InternaEdital = 5,
}
