namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirOpcoesDeCurso"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota). <c>Numero</c> é <c>[JsonRequired]</c>: omiti-lo é 400, e não
/// um zero que o agregado recusaria como fora de 1 ou 2.
/// </summary>
public sealed record DefinirOpcoesDeCursoRequest([property: JsonRequired] int Numero);
