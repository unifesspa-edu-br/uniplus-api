namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// DTO de uma versão promovida e imutável de <c>TermoConsentimento</c>.
/// </summary>
/// <remarks>
/// Sem <c>PromovidaPor</c> — ver <see cref="TermoConsentimentoDto"/>: o endpoint de
/// leitura é anônimo e esse campo carrega o identificador de autenticação do
/// operador que promoveu a versão.
/// </remarks>
public sealed record TermoConsentimentoVersaoDto(
    Guid Id,
    string Texto,
    string BaseLegal,
    string FormaAceite,
    string Hash,
    DateTimeOffset PromovidaEm);
