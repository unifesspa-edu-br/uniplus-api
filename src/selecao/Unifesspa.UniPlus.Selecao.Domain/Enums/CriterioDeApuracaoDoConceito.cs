namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum CriterioDeApuracaoDoConceito
{
    [CodigoDeContrato("UNANIMIDADE", "Unanimidade", Descricao = "Unanimidade.")]
    Unanimidade = 1,

    [CodigoDeContrato("MAIORIA_SIMPLES", "Maioria simples", Descricao = "Maioria simples.")]
    MaioriaSimples = 2,

    [CodigoDeContrato("MAIORIA_ABSOLUTA", "Maioria absoluta", Descricao = "Maioria absoluta.")]
    MaioriaAbsoluta = 3,
}
