namespace Unifesspa.UniPlus.Selecao.API.Contracts.Responses;

/// <summary>
/// Item do checklist estrutural que reprovou, como aparece em
/// <c>ProblemDetails.extensions.pendencias</c> na recusa da publicação.
/// </summary>
/// <remarks>
/// Carrega a mesma identidade que <c>GET /conformidade</c> publica, de propósito: um preview
/// estrutural aprovado que ficou obsoleto e a recusa de publicação que veio depois precisam
/// ser comparáveis por código e dimensão. Antes daqui a extensão levava só o texto, e
/// qualquer link para a seção correta do editor dependeria de o cliente reconhecer a frase.
/// </remarks>
/// <param name="Codigo">Identificador estável da invariante reprovada.</param>
/// <param name="Dimensao">Seção do agregado onde a pendência se corrige.</param>
/// <param name="Mensagem">Texto humano da pendência.</param>
public sealed record PendenciaEstruturalDto(string Codigo, string Dimensao, string Mensagem);
