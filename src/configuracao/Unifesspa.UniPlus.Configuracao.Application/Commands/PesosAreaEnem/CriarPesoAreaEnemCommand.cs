namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma linha de pesos do ENEM por grupo de área: a resolução, o grupo de
/// área, os cinco pesos das áreas de conhecimento, o corte de redação (assume
/// 400 quando omitido) e a base legal. Os atores de auditoria (<c>created_by</c>)
/// são carimbados server-side via <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// Os cinco pesos são <c>[JsonRequired]</c>: como são <c>decimal</c> não-anuláveis,
/// omiti-los no JSON faria o System.Text.Json construir o record com <c>0m</c> — um
/// peso válido — em vez de rejeitar o campo ausente. O <c>CorteRedacao</c> é o único
/// genuinamente opcional (assume 400 quando omitido).
/// </remarks>
public sealed record CriarPesoAreaEnemCommand(
    string Resolucao,
    string GrupoCurso,
    [property: JsonRequired] decimal PesoRedacao,
    [property: JsonRequired] decimal PesoCienciasNatureza,
    [property: JsonRequired] decimal PesoCienciasHumanas,
    [property: JsonRequired] decimal PesoLinguagens,
    [property: JsonRequired] decimal PesoMatematica,
    string BaseLegal,
    decimal? CorteRedacao = null) : ICommand<Result<Guid>>;
