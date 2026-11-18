namespace Unifesspa.UniPlus.Selecao.Application.Queries.Rascunhos;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>Exporta o documento do rascunho, com o ETag da revisão lida.</summary>
/// <param name="Id">O identificador do rascunho.</param>
public sealed record ObterRascunhoDeProcessoSeletivoQuery(Guid Id) : IQuery<Result<RascunhoExportadoDto>>;
