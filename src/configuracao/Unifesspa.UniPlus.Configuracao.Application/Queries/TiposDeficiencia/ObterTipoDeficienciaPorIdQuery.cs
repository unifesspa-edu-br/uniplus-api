namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDeficiencia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterTipoDeficienciaPorIdQuery(Guid Id) : IQuery<Result<TipoDeficienciaDto>>;
