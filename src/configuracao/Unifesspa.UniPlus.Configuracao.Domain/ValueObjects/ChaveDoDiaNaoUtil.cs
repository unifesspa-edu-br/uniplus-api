namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Identidade de um dia não útil dentro do dataset: a mesma data, abrangência, município e UF não se repetem.
/// </summary>
internal readonly record struct ChaveDoDiaNaoUtil(DateOnly Data, Abrangencia Abrangencia, string? MunicipioIbge, string? Uf);
