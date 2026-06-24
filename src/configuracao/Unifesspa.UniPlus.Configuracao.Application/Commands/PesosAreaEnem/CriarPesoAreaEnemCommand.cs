namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma linha de pesos do ENEM por grupo de área: a resolução, o grupo de
/// área, os cinco pesos das áreas de conhecimento, o corte de redação (assume
/// 400 quando omitido) e a base legal. Os atores de auditoria (<c>created_by</c>)
/// são carimbados server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarPesoAreaEnemCommand(
    string Resolucao,
    string GrupoCurso,
    decimal PesoRedacao,
    decimal PesoCienciasNatureza,
    decimal PesoCienciasHumanas,
    decimal PesoLinguagens,
    decimal PesoMatematica,
    string BaseLegal,
    decimal? CorteRedacao = null) : ICommand<Result<Guid>>;
