namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Leitura do cadastro de reserva de uma chamada, no formato de cada item do corpo de <c>PUT /cadastro-reserva</c>. A
/// quantidade conta só as pessoas da reserva, e zero é a chamada sem reserva.
/// </summary>
public sealed record CadastroDeReservaDaChamadaDto(int NumeroDaChamada, int PessoasDaReservaPorVaga);
