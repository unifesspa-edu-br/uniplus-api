namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

public sealed record ObterTipoAtoPublicadoPorIdQuery(Guid Id) : IQuery<TipoAtoPublicadoDto?>;
