namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>Lista as gerações do arquivo de envio ao INEP de uma edição, da mais recente para a mais antiga.</summary>
public sealed record ListarEnviosAoInepQuery(Guid ProcessoSeletivoId, Guid EtapaProcessoId, string Edicao)
    : IQuery<Result<IReadOnlyList<GeracaoDeEnvioDto>>>;
