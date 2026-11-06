namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Novos identificadores de uma Unidade, a data em que valem e o motivo da troca. A data vem do
/// handler, porque o domínio não consulta relógio.
/// </summary>
public readonly record struct MudancaDeIdentificadores(IdentificadoresDaUnidade Novos, DateOnly Data, string? Motivo);
