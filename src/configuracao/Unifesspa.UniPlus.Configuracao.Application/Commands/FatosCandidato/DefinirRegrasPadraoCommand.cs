namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Uma regra padrão: quando o predicado é verdadeiro, contribui <see cref="Contribui"/> ao
/// categórico, ou torna verdadeiro o booleano, que não informa <see cref="Contribui"/>. O
/// <see cref="Quando"/> é o OU de cláusulas, cada uma o E de condições; nulo ou vazio é a regra
/// incondicional.
/// </summary>
public sealed record RegraPadraoInput(
    string? Contribui,
    IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>>? Quando);

/// <summary>Substitui as regras padrão de um derivado por regra do administrador (ADR-0136).</summary>
public sealed record DefinirRegrasPadraoCommand(Guid Id, IReadOnlyList<RegraPadraoInput> Regras) : ICommand<Result>;

/// <summary>
/// Corpo da definição das regras padrão de um derivado. A lista é obrigatória: só a lista vazia
/// explícita remove as regras, nunca um corpo sem ela.
/// </summary>
public sealed record RegrasPadraoInput([property: JsonRequired] IReadOnlyList<RegraPadraoInput> Regras);
