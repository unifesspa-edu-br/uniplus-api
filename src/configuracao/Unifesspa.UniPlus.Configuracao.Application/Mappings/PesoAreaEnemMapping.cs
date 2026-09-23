namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class PesoAreaEnemMapping
{
    public static PesoAreaEnemDto ToDto(this PesoAreaEnem peso)
    {
        ArgumentNullException.ThrowIfNull(peso);
        return new PesoAreaEnemDto(
            peso.Id,
            peso.Resolucao,
            peso.GrupoCurso.Valor,
            [.. peso.AreasDaLinha.Select(static area =>
                new PesoAreaEnemAreaDto(area.Codigo, area.Rotulo, area.Peso, area.Corte))],
            peso.BaseLegal,
            peso.CreatedAt);
    }
}
