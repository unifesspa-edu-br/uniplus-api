namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Converte as áreas do payload para a forma que o agregado valida. Um item nulo no JSON
/// (<c>[null]</c>) vira área sem código, para voltar como erro do campo daquele índice em
/// vez de derrubar a requisição.
/// </summary>
internal static class AreasDoComando
{
    public static IReadOnlyList<AreaInformada>? ParaDominio(IReadOnlyList<PesoAreaEnemAreaCommand>? areas) =>
        areas is null
            ? null
            : [.. areas.Select(static area => area is null
                ? new AreaInformada(null, 0m, null)
                : new AreaInformada(area.Codigo, area.Peso, area.Corte))];
}
