namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

/// <summary>Um ponto do documento enviado, com o que o esquema diz dele e o lugar que ocupa.</summary>
/// <param name="Valor">O valor no documento.</param>
/// <param name="Forma">O que o esquema declara para o ponto.</param>
/// <param name="Ponteiro">O JSON Pointer do ponto no documento enviado.</param>
/// <param name="ParteAncestral">O ponteiro da parte com chave que contém o ponto, ou vazio quando nenhuma o contém.</param>
internal readonly record struct NoDoDocumento(JsonNode Valor, FormaDoNo Forma, string Ponteiro, string ParteAncestral);
