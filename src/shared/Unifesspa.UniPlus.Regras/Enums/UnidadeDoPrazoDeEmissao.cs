namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

[DescricaoDoContrato("Unidade de contagem do prazo de emissão.")]
public enum UnidadeDoPrazoDeEmissao
{
    [CodigoDeContrato("DIAS", "Dias")]
    Dias = 1,

    [CodigoDeContrato("MESES", "Meses")]
    Meses = 2,

    [CodigoDeContrato("ANOS", "Anos")]
    Anos = 3,
}
