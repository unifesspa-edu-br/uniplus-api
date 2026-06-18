namespace Unifesspa.UniPlus.Geo.Application.Mappings;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Domain.Entities;

public static class HierarquiaMapping
{
    public static DistritoDto ToDto(this Distrito distrito, string cidadeCodigoIbge)
    {
        ArgumentNullException.ThrowIfNull(distrito);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidadeCodigoIbge);

        return new DistritoDto(
            distrito.Id,
            distrito.Nome,
            distrito.Uf,
            cidadeCodigoIbge,
            distrito.Latitude,
            distrito.Longitude);
    }

    public static BairroDto ToDto(this Bairro bairro, string cidadeCodigoIbge)
    {
        ArgumentNullException.ThrowIfNull(bairro);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidadeCodigoIbge);

        return new BairroDto(
            bairro.Id,
            bairro.Nome,
            bairro.Uf,
            cidadeCodigoIbge,
            bairro.Latitude,
            bairro.Longitude);
    }

    public static LogradouroResumoDto ToDto(this LogradouroComBairro linha, string cidadeCodigoIbge)
    {
        ArgumentNullException.ThrowIfNull(linha);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidadeCodigoIbge);

        Logradouro logradouro = linha.Logradouro;
        return new LogradouroResumoDto(
            logradouro.Id,
            logradouro.Cep,
            logradouro.Tipo,
            logradouro.Nome,
            logradouro.NomeCompleto,
            linha.BairroNome,
            cidadeCodigoIbge,
            logradouro.Uf);
    }
}
