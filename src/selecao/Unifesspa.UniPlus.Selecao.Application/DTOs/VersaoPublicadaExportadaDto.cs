namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Nodes;

public sealed record VersaoPublicadaExportadaDto(JsonObject Documento, string ETag);
