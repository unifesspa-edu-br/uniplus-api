namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CondicoesAtendimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterCondicaoAtendimentoPorIdQuery(Guid Id) : IQuery<CondicaoAtendimentoDto?>;
