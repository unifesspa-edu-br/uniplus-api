namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Entrada de criação de um <see cref="DiaNaoUtil"/>, usada só por
/// <see cref="CalendarioDiasUteis.Criar"/>. <c>Abrangencia</c> é o token canônico
/// UPPER_SNAKE (ex.: <c>NACIONAL</c>, <c>MUNICIPAL</c>), analisado internamente.
/// <c>MunicipioIbge</c>, <c>MunicipioNome</c> e <c>MunicipioUf</c> formam um
/// snapshot atômico obrigatório apenas para abrangência MUNICIPAL.
/// <c>Uf</c> é obrigatória para abrangência ESTADUAL — sem ela, feriados de
/// estados diferentes seriam indistinguíveis para o consumidor cross-módulo.
/// </summary>
/// <remarks>
/// <c>Abrangencia</c> e <c>Descricao</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem validator FluentValidation garantindo não-nulo a montante,
/// um item malformado no payload chega com esses campos nulos, e a validação
/// de domínio (não o model binding) precisa ser quem recusa.
/// </remarks>
public sealed record DiaNaoUtilCriacao(
    string? Abrangencia,
    string? MunicipioIbge,
    string? MunicipioNome,
    string? MunicipioUf,
    DateOnly Data,
    string? Descricao,
    string? Uf = null);

/// <summary>Dia não útil já validado e com os campos analisados/normalizados, pronto para <see cref="DiaNaoUtil.Abrir"/>.</summary>
internal sealed record DiaNaoUtilResolvido(
    Abrangencia Abrangencia,
    string? MunicipioIbge,
    string? MunicipioNome,
    string? MunicipioUf,
    string? Uf,
    DateOnly Data,
    string Descricao);
