namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os dois ramos em que uma regra de distribuição de vagas monta o quadro, com o código que o contrato REST
/// publica: o cálculo da Lei de Cotas (art. 10) ou o quadro que o edital fixa. O cliente escolhe os campos
/// da distribuição por este ramo, e não por uma lista de códigos de regra copiada no código dele.
/// </summary>
public static class RamoDaDistribuicaoDeVagasCodigo
{
    public const string LeiDeCotas = "LEI_DE_COTAS";

    public const string QuadroFixadoNoEdital = "QUADRO_FIXADO_NO_EDITAL";
}
