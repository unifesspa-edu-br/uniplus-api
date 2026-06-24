namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza os cinco pesos, o corte de redação e a base legal de uma linha de
/// pesos. A chave de negócio (<c>Resolucao</c> + <c>GrupoCurso</c>) e o <c>Id</c>
/// são imutáveis (CA-04b) — não constam no payload.
/// </summary>
/// <remarks>
/// PUT é substituição completa: o <c>CorteRedacao</c> é obrigatório (como os cinco
/// pesos), não opcional. Diferente do <c>Criar</c> — que assume 400 quando omitido —
/// aqui já existe um valor configurado; aceitar omissão faria o corte cair
/// silenciosamente para 400, sobrescrevendo o valor atual. O cliente sempre reenvia
/// o corte vigente.
/// </remarks>
public sealed record AtualizarPesoAreaEnemCommand(
    Guid Id,
    decimal PesoRedacao,
    decimal PesoCienciasNatureza,
    decimal PesoCienciasHumanas,
    decimal PesoLinguagens,
    decimal PesoMatematica,
    decimal CorteRedacao,
    string BaseLegal) : ICommand<Result>;
