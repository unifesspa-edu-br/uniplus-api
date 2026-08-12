namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Fundamento de isenção de taxa de inscrição (issue #1112). Vocabulário fechado —
/// os dois fundamentos hoje decididos pelo PO. Referenciar um fundamento na
/// configuração não decide origem do fato, forma de comprovação, deferimento
/// automático nem análise humana — isso pertence à frente de inscrição.
/// </summary>
public enum FundamentoIsencao
{
    Nenhum = 0,
    CadastroUnico = 1,
    DoacaoMedulaOssea = 2,
}
