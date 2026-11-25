namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

internal static class DiaNaoUtilCriacaoDeTeste
{
    internal static readonly DateOnly DataBase = new(2027, 1, 1);

    internal static DiaNaoUtilCriacao Nacional(DateOnly? data = null) =>
        new("NACIONAL", null, null, null, data ?? DataBase, "Confraternização Universal");

    internal static DiaNaoUtilCriacao Estadual(DateOnly? data = null, string uf = "PA") =>
        new("ESTADUAL", null, null, null, data ?? DataBase.AddDays(1), "Data magna do estado", uf);

    internal static DiaNaoUtilCriacao Municipal(
        DateOnly? data = null,
        string? municipioIbge = "1501402",
        string? municipioNome = "Belém",
        string? municipioUf = "PA") =>
        new(
            "MUNICIPAL",
            municipioIbge,
            municipioNome,
            municipioUf,
            data ?? DataBase.AddDays(2),
            "Aniversário do município");

    internal static DiaNaoUtilCriacao Institucional(DateOnly? data = null) =>
        new("INSTITUCIONAL", null, null, null, data ?? DataBase.AddDays(3), "Recesso da Unifesspa");
}
