namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resolve o ruleset de obrigatoriedades legais aplicável a um processo numa
/// data de referência explícita. É uma capacidade interna para o gate de
/// publicação; não expõe endpoint próprio.
/// </summary>
public sealed record ObterObrigatoriedadesAplicaveisQuery(
    Guid ProcessoSeletivoId,
    DateOnly DataReferencia) : IQuery<IReadOnlyList<ObrigatoriedadeLegalDto>>;
