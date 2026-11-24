namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Quando a data de uma <see cref="Entities.FaseCronograma"/> é fixada — cópia (ADR-0061) do
/// <c>OrigemData</c> vigente na fase canônica de origem (módulo Configuração) no momento em que a
/// fase entrou no cronograma.
/// </summary>
/// <remarks>
/// Enum <b>próprio</b> do Domain de Seleção — não é referência cross-módulo ao enum
/// homônimo de Configuração (ADR-0042: Domain não depende de outro módulo). Mesmos
/// códigos, dois tipos distintos.
/// <para>
/// O período obrigatório ou opcional da fase decorre <b>apenas</b> deste atributo (CA-07):
/// <see cref="NoEdital"/> exige <c>Inicio</c>/<c>Fim</c>; <see cref="NaExecucao"/> aceita
/// "sem data" como estado válido.
/// </para>
/// </remarks>
public enum OrigemDataFase
{
    [CodigoDeContrato(
        "NO_EDITAL",
        "No edital",
        Descricao = "A data é fixada no edital: o período é obrigatório.")]
    NoEdital = 1,

    [CodigoDeContrato(
        "NA_EXECUCAO",
        "Na execução do processo",
        Descricao = "A data não é fixada no edital: a unidade responsável pela fase a define na execução, e o período é opcional.")]
    NaExecucao = 2,
}
