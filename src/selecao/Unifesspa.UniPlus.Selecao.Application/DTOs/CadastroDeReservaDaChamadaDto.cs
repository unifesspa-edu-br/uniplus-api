namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>
/// Leitura do cadastro de reserva de uma chamada, no formato de cada item do corpo de <c>PUT /cadastro-reserva</c>. A
/// quantidade conta só as pessoas da reserva, e zero é a chamada sem reserva. A participação da reserva só existe na
/// chamada com reserva e vem com o rótulo, que é só de leitura.
/// </summary>
public sealed record CadastroDeReservaDaChamadaDto(
    int NumeroDaChamada,
    int PessoasDaReservaPorVaga,
    CodigoComRotulo<ParticipacaoDaReserva>? ParticipacaoDaReserva);
