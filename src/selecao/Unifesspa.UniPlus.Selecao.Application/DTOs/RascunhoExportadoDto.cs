namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Nodes;

/// <summary>O documento do rascunho como a exportação o entrega, com o ETag da revisão lida.</summary>
/// <param name="Documento">O documento guardado, com o <c>id</c> do rascunho na raiz e os descritivos de identificação só de leitura.</param>
/// <param name="ETag">O ETag forte da revisão lida, que a substituição e a exclusão devolvem em <c>If-Match</c>.</param>
public sealed record RascunhoExportadoDto(JsonObject Documento, string ETag);
