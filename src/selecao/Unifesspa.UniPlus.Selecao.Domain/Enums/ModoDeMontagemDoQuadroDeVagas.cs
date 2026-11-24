namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum ModoDeMontagemDoQuadroDeVagas
{
    [CodigoDeContrato("LEI_DE_COTAS", "Cálculo pela Lei de Cotas", Descricao = "Calcula as vagas conforme a Lei de Cotas.")]
    LeiDeCotas = 1,
    [CodigoDeContrato("QUADRO_FIXADO_NO_EDITAL", "Quadro fixado no edital", Descricao = "Usa as quantidades de vagas fixadas no edital.")]
    QuadroFixadoNoEdital = 2,
}
