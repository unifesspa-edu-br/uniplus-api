namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="NotasDoEnemController.GerarEnvio"/>: o leiaute do arquivo de envio, entre os
/// ativos do catálogo.
/// </summary>
public sealed record GerarEnvioAoInepRequest(string? LeiauteCodigo, string? LeiauteVersao);
