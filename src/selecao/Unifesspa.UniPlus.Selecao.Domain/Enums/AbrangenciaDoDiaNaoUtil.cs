namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Onde incide um dia não útil.</summary>
public enum AbrangenciaDoDiaNaoUtil
{
    [CodigoDeContrato("NACIONAL", "Nacional", Descricao = "Incide em todo o país.")]
    Nacional = 1,
    [CodigoDeContrato("ESTADUAL", "Estadual", Descricao = "Incide no estado informado.")]
    Estadual = 2,
    [CodigoDeContrato("MUNICIPAL", "Municipal", Descricao = "Incide no município informado.")]
    Municipal = 3,
    [CodigoDeContrato("INSTITUCIONAL", "Institucional", Descricao = "Incide na instituição.")]
    Institucional = 4,
}
