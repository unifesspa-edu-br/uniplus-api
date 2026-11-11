namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Os motivos que o processo escolheu, por tipo e código.</summary>
public sealed record ListarMotivosDoProcessoQuery(Guid ProcessoSeletivoId) : IQuery<IReadOnlyList<MotivoDoProcessoDto>>;
