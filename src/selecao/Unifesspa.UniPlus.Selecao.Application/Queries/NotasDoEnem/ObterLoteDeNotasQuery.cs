namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>Um lote de notas da edição, com as ocorrências.</summary>
public sealed record ObterLoteDeNotasQuery(Guid ProcessoSeletivoId, Guid EtapaProcessoId, string Edicao, Guid LoteId)
    : IQuery<Result<LoteDeNotasDoEnemDto>>;
