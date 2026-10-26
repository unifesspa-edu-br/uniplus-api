namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>Lista os lotes de notas da edição, do mais recente para o mais antigo, sem as ocorrências.</summary>
public sealed record ListarLotesDeNotasQuery(Guid ProcessoSeletivoId, Guid EtapaProcessoId, string Edicao)
    : IQuery<Result<IReadOnlyList<LoteDeNotasDoEnemDto>>>;
