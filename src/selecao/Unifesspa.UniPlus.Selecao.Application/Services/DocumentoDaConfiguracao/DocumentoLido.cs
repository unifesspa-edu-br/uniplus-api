namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

/// <summary>O documento de configuração que passou pela forma, já normalizado, e o que dele a aplicação lê sem abrir o conteúdo.</summary>
/// <param name="Documento">O documento normalizado: sem os campos só de leitura e com a chave de cada parte.</param>
/// <param name="Nome">O nome do processo em <c>/identificacao/nome</c>, ou <see langword="null"/> enquanto não declarado.</param>
/// <param name="UnidadeAdministradoraCodigo">O código da unidade em <c>/identificacao/unidadeAdministradoraCodigo</c>, ou <see langword="null"/> enquanto não declarado.</param>
public sealed record DocumentoLido(JsonObject Documento, string? Nome, string? UnidadeAdministradoraCodigo);
