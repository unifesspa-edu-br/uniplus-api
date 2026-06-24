namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza os cinco pesos, o corte de redação e a base legal de uma linha de
/// pesos. A chave de negócio (<c>Resolucao</c> + <c>GrupoCurso</c>) e o <c>Id</c>
/// são imutáveis (CA-04b) — não constam no payload.
/// </summary>
public sealed record AtualizarPesoAreaEnemCommand(
    Guid Id,
    decimal PesoRedacao,
    decimal PesoCienciasNatureza,
    decimal PesoCienciasHumanas,
    decimal PesoLinguagens,
    decimal PesoMatematica,
    decimal? CorteRedacao,
    string BaseLegal) : ICommand<Result>;
