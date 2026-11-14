namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// O que identifica uma requisição idempotente no cache: quem pediu, em que endpoint, com qual chave, e o resumo do corpo
/// que a chave está presa a.
/// </summary>
internal sealed record ChaveDaRequisicao(string Scope, string Endpoint, string Chave, string ResumoDoCorpo);
