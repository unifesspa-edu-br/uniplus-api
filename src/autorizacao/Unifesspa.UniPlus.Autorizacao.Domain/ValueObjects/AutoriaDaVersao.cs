namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Quem pratica um ato sobre uma versão do perfil, como o token o identifica, e o que a trilha registra dele: o e-mail
/// verificado, normalizado, e se a pessoa tem o perfil. Ter o perfil não impede o ato (ADR-0157).
/// </summary>
/// <param name="Pessoa">Emissor, <c>sub</c> e nome de exibição.</param>
/// <param name="Email">E-mail verificado do token; nulo quando o token não o traz ou ele não está verificado.</param>
/// <param name="TemConcessao">A pessoa tem concessão do perfil, ainda não revogada nem vencida.</param>
public sealed record AutoriaDaVersao(Identidade Pessoa, string? Email, bool TemConcessao);
