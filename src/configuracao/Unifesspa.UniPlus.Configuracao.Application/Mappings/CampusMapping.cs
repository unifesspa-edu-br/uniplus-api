namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class CampusMapping
{
    public static CampusDto ToDto(this Campus campus)
    {
        ArgumentNullException.ThrowIfNull(campus);
        return new CampusDto(
            campus.Id,
            campus.Sigla,
            campus.Nome,
            campus.CidadeCodigoIbge,
            campus.CidadeNome,
            campus.CidadeUf,
            campus.CidadeOrigem,
            campus.CidadeDisplayAtualizadoEm,
            campus.Endereco,
            campus.Cep,
            campus.Latitude,
            campus.Longitude,
            campus.CodigoEmec,
            campus.CreatedAt);
    }
}
