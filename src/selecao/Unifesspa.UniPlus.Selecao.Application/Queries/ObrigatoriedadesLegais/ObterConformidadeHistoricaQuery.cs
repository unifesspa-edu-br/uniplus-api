namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Lê o snapshot imutável de conformidade gravado em
/// <c>edital_governance_snapshot.regras_json</c> no momento de
/// <c>Edital.Publicar()</c> (ADR-0058 §"Snapshot-on-bind"). Endpoint
/// <c>GET /api/selecao/editais/{id}/conformidade-historica</c>.
/// </summary>
public sealed record ObterConformidadeHistoricaQuery(Guid EditalId) : IQuery<ConformidadeDto?>;
