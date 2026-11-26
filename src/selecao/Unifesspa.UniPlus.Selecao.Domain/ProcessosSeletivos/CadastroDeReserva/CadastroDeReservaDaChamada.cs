namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O cadastro de reserva de uma chamada: quantas pessoas da reserva ela convoca por vaga, além de quem
/// ocupa a vaga (UNI-REQ-0355). A quantidade conta só a reserva, e zero é a chamada sem reserva. A chamada com
/// reserva declara a participação da reserva (UNI-REQ-0505), e a chamada sem reserva fica sem ela.
/// </summary>
public sealed record CadastroDeReservaDaChamada(NumeroDaChamada Chamada, int PessoasDaReservaPorVaga, ParticipacaoDaReserva? Participacao);

/// <summary>
/// O cadastro de reserva de uma chamada como a configuração o informa, ainda por conferir. A participação vem como o
/// código de contrato de <see cref="ParticipacaoDaReserva"/>, e é o domínio que recusa o código desconhecido.
/// </summary>
public sealed record CadastroDeReservaInformado(
    int? NumeroDaChamada,
    int? PessoasDaReservaPorVaga,
    [property: AceitaCodigosDe<ParticipacaoDaReserva>] string? ParticipacaoDaReserva);
