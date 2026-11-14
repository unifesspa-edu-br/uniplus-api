namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Consulta um ato do processo, com os documentos, na mesma projeção que a lista dos atos do processo traz.
/// </summary>
public sealed record ObterAtoDoProcessoQuery(Guid ProcessoSeletivoId, Guid AtoId) : IQuery<Result<AtoDoProcessoDto>>;
