namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// Lista os leiautes de arquivo do INEP ativos, de um tipo ou de todos. O leiaute desativado sai da
/// escolha, e o lote que o usou continua a citá-lo.
/// </summary>
public sealed record ListarLeiautesDoInepQuery(TipoDeLeiauteDoInep? Tipo) : IQuery<IReadOnlyList<LeiauteDoInepDto>>;
