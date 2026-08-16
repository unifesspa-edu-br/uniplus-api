namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

/// <summary>
/// Item de entrada de um dia não útil no payload de criação. <c>Abrangencia</c> é o
/// token canônico UPPER_SNAKE (ex.: <c>NACIONAL</c>, <c>MUNICIPAL</c>) — a análise
/// e a validação de campo ocorrem no agregado (<c>CalendarioDiasUteis.Criar</c>).
/// A tripla <c>MunicipioIbge</c>/<c>MunicipioNome</c>/<c>MunicipioUf</c> é obrigatória
/// para MUNICIPAL; <c>Uf</c> continua exclusiva da abrangência ESTADUAL.
/// </summary>
/// <remarks>
/// <c>Abrangencia</c> e <c>Descricao</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem validator FluentValidation garantindo não-nulo a montante,
/// o model binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record DiaNaoUtilCommandItem(
    string? Abrangencia,
    string? MunicipioIbge,
    string? MunicipioNome,
    string? MunicipioUf,
    DateOnly Data,
    string? Descricao,
    string? Uf = null);
