namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Por que as inscrições de um processo seletivo não estão abertas para o candidato naquele instante.
/// Só há motivo quando as inscrições estão fechadas; com elas abertas, não há valor a informar.
/// </summary>
public enum MotivoDasInscricoesFechadas
{
    [CodigoDeContrato("PERIODO_ENCERRADO", "Período de inscrição encerrado", Descricao = "O período de inscrição do processo seletivo já terminou.")]
    PeriodoEncerrado = 1,
    [CodigoDeContrato("PERIODO_NAO_INICIADO", "Período de inscrição não iniciado", Descricao = "O período de inscrição do processo seletivo ainda não começou.")]
    PeriodoNaoIniciado = 2,
    [CodigoDeContrato("PROCESSO_SEM_INSCRICAO", "Processo sem inscrição pelo sistema", Descricao = "O processo seletivo não recebe inscrição pelo sistema.")]
    ProcessoSemInscricao = 3,
    [CodigoDeContrato("PERIODO_NAO_CONFIRMADO", "Período de inscrição não confirmado", Descricao = "O período de inscrição do processo seletivo não pôde ser lido agora.")]
    PeriodoNaoConfirmado = 4,
}
