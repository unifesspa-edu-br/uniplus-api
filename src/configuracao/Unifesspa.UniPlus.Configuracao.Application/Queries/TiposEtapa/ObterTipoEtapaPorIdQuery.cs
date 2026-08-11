namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterTipoEtapaPorIdQuery(Guid Id) : IQuery<TipoEtapaDto?>;
