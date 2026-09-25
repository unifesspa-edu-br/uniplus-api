namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirIdentificadorLegivel"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// Kebab-case de 3 a 64 caracteres, minúsculas sem acento, começando por letra e terminando por
/// letra ou dígito, e sem a forma de um Guid. Ausente remove a declaração. Aceito enquanto o
/// identificador não consta em versão publicada.
/// </remarks>
public sealed record DefinirIdentificadorLegivelRequest(string? IdentificadorLegivel);
