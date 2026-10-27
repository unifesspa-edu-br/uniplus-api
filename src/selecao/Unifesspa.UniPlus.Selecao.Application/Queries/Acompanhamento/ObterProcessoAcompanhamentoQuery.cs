namespace Unifesspa.UniPlus.Selecao.Application.Queries.Acompanhamento;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

public sealed record ObterProcessoAcompanhamentoQuery(Guid Id) : IQuery<ProcessoAcompanhamentoResumoDto?>;
