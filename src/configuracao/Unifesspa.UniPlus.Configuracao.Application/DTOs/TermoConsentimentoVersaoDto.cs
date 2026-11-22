namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

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
    int Numero,
    string Texto,
    string BaseLegal,
    CodigoComRotulo<FormaAceite> FormaAceite,
    string Hash,
    DateTimeOffset PromovidaEm);
