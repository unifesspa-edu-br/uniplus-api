namespace Unifesspa.UniPlus.Geo.Application.Mappings;

using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Domain.Entities;

public static class CidadeMapping
{
    public static CidadeResumoDto ToResumoDto(this Cidade cidade)
    {
        ArgumentNullException.ThrowIfNull(cidade);
        return new CidadeResumoDto(
            cidade.Id,
            cidade.CodigoIbge,
            cidade.Nome,
            cidade.Uf,
            cidade.Ddd);
    }

    public static CidadeDetalheDto ToDetalheDto(this Cidade cidade, CidadeIndicador? indicador)
    {
        ArgumentNullException.ThrowIfNull(cidade);
        return new CidadeDetalheDto(
            cidade.Id,
            cidade.CodigoIbge,
            cidade.Nome,
            cidade.Uf,
            cidade.Ddd,
            cidade.Latitude,
            cidade.Longitude,
            cidade.MesorregiaoNome,
            cidade.MicrorregiaoNome,
            cidade.RegiaoIntermediariaNome,
            cidade.RegiaoImediataNome,
            indicador?.ToIndicadorDto());
    }

    public static CidadeIndicadorDto ToIndicadorDto(this CidadeIndicador indicador)
    {
        ArgumentNullException.ThrowIfNull(indicador);
        return new CidadeIndicadorDto(
            indicador.Gentilico,
            indicador.AreaKm2,
            indicador.PopulacaoResidente,
            indicador.DensidadeDemografica,
            indicador.Idh,
            indicador.Aniversario);
    }
}
