namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Fundamento de isenção de taxa de inscrição (issue #1112). Vocabulário fechado.
/// Referenciar um fundamento na configuração não decide origem do fato, forma de
/// comprovação, deferimento automático nem análise humana — isso pertence à
/// frente de inscrição.
/// </summary>
public enum FundamentoIsencao
{
    Nenhum = 0,
    CadastroUnico = 1,
    DoacaoMedulaOssea = 2,

    /// <summary>
    /// Carência socioeconômica na acepção da Lei nº 12.799/2013, que é a norma
    /// que rege isenção em processo seletivo de ingresso em IFES. Os dois
    /// fundamentos acima vêm da prática institucional e se apoiam em normas de
    /// concurso público, que não alcançam vestibular (issue #1296).
    /// </summary>
    CarenciaSocioeconomica = 3,
}
