namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>
/// O cadastro de reserva de uma chamada: quantas pessoas da reserva ela convoca por vaga, além de quem
/// ocupa a vaga (UNI-REQ-0355). A quantidade conta só a reserva.
/// </summary>
public sealed record CadastroDeReservaDaChamada(NumeroDaChamada Chamada, int PessoasDaReservaPorVaga);

/// <summary>O cadastro de reserva de uma chamada como a configuração o informa, ainda por conferir.</summary>
public sealed record CadastroDeReservaInformado(int? NumeroDaChamada, int? PessoasDaReservaPorVaga);
