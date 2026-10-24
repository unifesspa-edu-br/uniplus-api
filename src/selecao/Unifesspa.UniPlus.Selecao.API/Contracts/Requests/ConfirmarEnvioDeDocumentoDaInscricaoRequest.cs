namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

/// <summary>Corpo da confirmação: o ETag da resposta do PUT, que fixa a versão do objeto que o verificador confere.</summary>
public sealed record ConfirmarEnvioDeDocumentoDaInscricaoRequest(string? Etag);
