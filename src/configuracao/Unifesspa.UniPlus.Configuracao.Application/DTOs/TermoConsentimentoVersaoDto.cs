namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// DTO de uma versão promovida e imutável de <c>TermoConsentimento</c>.
/// </summary>
public sealed record TermoConsentimentoVersaoDto(
    Guid Id,
    string Texto,
    string BaseLegal,
    string FormaAceite,
    string Hash,
    string PromovidaPor,
    DateTimeOffset PromovidaEm);
