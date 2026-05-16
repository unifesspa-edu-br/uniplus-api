namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Avalia a conformidade do edital contra o ruleset atual (regras vigentes
/// aplicáveis ao tipo). Endpoint <c>GET /api/selecao/editais/{id}/conformidade</c>
/// — consumido pelo wizard de edital para o passo 12 (Revisão) per ADR-0058.
/// </summary>
public sealed record ObterConformidadeAtualQuery(Guid EditalId) : IQuery<ConformidadeDto?>;
