namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O que a reserva de uma chamada faz quando a chamada a convoca, escolhido pelo edital para cada chamada que tem
/// pessoas da reserva (UNI-REQ-0505). Não há valor padrão: a chamada com reserva declara uma das duas opções.
/// </summary>
public enum ParticipacaoDaReserva
{
    /// <summary>A pessoa da reserva entrega os documentos e passa pelas bancas na chamada, mas não é homologada como apta à matrícula nem ocupa a vaga.</summary>
    [CodigoDeContrato("ANTECIPA_A_HABILITACAO", "A reserva só antecipa a habilitação",
        Descricao = "A pessoa da reserva sai da chamada com a documentação deferida, sem ser homologada como apta à matrícula; a vaga que sobrar segue para a chamada seguinte.")]
    AntecipaAHabilitacao = 1,

    /// <summary>A pessoa da reserva homologada como habilitada ocupa a vaga na própria chamada.</summary>
    [CodigoDeContrato("ASSUME_NA_MESMA_CHAMADA", "A reserva assume na mesma chamada",
        Descricao = "A pessoa da reserva homologada como habilitada ocupa a vaga na própria chamada.")]
    AssumeNaMesmaChamada = 2,
}
