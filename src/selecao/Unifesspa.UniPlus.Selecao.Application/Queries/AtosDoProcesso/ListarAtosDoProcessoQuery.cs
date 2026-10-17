namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lista os atos do processo — os que criaram versão do edital e os registros dos que não criam —,
/// com a situação e os documentos de cada um.
/// </summary>
public sealed record ListarAtosDoProcessoQuery(Guid ProcessoSeletivoId) : IQuery<Result<ListarAtosDoProcessoResult>>;
