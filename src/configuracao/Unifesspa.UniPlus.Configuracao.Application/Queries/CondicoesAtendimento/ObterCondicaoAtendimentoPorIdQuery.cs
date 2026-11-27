namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CondicoesAtendimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterCondicaoAtendimentoPorIdQuery(Guid Id) : IQuery<Result<CondicaoAtendimentoDto>>;
