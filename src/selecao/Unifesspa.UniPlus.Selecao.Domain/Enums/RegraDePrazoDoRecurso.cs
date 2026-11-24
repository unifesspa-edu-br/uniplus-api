namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum RegraDePrazoDoRecurso
{
    [CodigoDeContrato("RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "Conta o prazo da publicação do ato", Descricao = "Conta o prazo de recurso a partir da publicação do ato.")]
    ContadoDaPublicacaoDoAto = 1,
}
