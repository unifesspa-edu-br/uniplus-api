namespace Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Corpo da requisição para disparar uma carga do ETL DNE (Story #674):
/// <c>POST /api/admin/geo/importacoes</c>. A versão é a release DNE no formato
/// AAAAMM (ex.: <c>202602</c>) a ser aplicada.
/// </summary>
public sealed record DispararImportacaoRequest(string Versao);
