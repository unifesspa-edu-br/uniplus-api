namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Quem faz a requisição, como o token a traz: identidade, nome e o e-mail com o que o provedor
/// afirma sobre ele. Nunca traz CPF.
/// </summary>
public sealed record PessoaNoToken(Identidade Identidade, string? Nome, string? Email, bool EmailVerificado);
