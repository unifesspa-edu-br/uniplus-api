namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

/// <summary>
/// Item de entrada de um dia não útil no payload de criação. <c>Abrangencia</c> é o
/// token canônico UPPER_SNAKE (ex.: <c>NACIONAL</c>, <c>MUNICIPAL</c>) — a análise
/// e a validação de campo (inclusive <c>Uf</c> obrigatória para ESTADUAL) ocorrem
/// no agregado (<c>CalendarioDiasUteis.Criar</c>).
/// </summary>
public sealed record DiaNaoUtilCommandItem(
    string Abrangencia,
    string? MunicipioIbge,
    DateOnly Data,
    string Descricao,
    string? Uf = null);
