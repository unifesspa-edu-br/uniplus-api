namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza o peso e o corte de cada área e a base legal de uma linha de Pesos por Área.
/// A chave de negócio (<c>Resolucao</c> + <c>GrupoCurso</c>) e o <c>Id</c> são imutáveis —
/// mudá-los caracterizaria outra linha, não uma edição — e não constam no payload.
/// </summary>
/// <remarks>
/// PUT é substituição completa: as cinco áreas vêm sempre, cada uma com o peso; o corte
/// ausente é "sem corte". <c>Areas</c> e <c>BaseLegal</c> são anuláveis sem default
/// (ADR-0125): nulo para o campo ausente chegar à validação de domínio em vez do
/// <c>[ApiController]</c>; sem default, para permanecerem <c>required</c> no schema OpenAPI.
/// </remarks>
public sealed record AtualizarPesoAreaEnemCommand(
    Guid Id,
    IReadOnlyList<PesoAreaEnemAreaCommand>? Areas,
    string? BaseLegal) : ICommand<Result>;
