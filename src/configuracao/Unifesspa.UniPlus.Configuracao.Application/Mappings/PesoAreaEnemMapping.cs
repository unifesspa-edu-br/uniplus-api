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
            peso.PesoRedacao,
            peso.PesoCienciasNatureza,
            peso.PesoCienciasHumanas,
            peso.PesoLinguagens,
            peso.PesoMatematica,
            peso.CorteRedacao,
            peso.BaseLegal,
            peso.CreatedAt);
    }
}
