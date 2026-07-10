namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>Obtém um ato pelo identificador, com os avisos de numeração recomputados (AC4).</summary>
public sealed record ObterAtoNormativoPorIdQuery(Guid Id) : IQuery<AtoNormativoDto?>;
