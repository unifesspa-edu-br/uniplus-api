namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>
/// Lê o que a reserva de uma chamada pode fazer quando a chamada a convoca, com o código, o rótulo e o que cada opção significa.
/// </summary>
public sealed record ListarParticipacoesDaReservaQuery : IQuery<IReadOnlyList<CodigoComDescricao<ParticipacaoDaReserva>>>;
