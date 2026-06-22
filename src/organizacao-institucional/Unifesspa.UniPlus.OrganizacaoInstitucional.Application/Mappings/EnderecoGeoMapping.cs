namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

/// <summary>
/// Composição dos sub-objetos aninhados do contrato HTTP (ADR-0096) a partir do
/// estado flat da Instituicao: a referência de cidade da sede (opcional, com
/// proveniência do display cache) e o endereço estruturado opcional.
/// </summary>
public static class EnderecoGeoMapping
{
    /// <summary>Cidade da sede (opcional all-or-nothing): trio + metadados, ou nulo.</summary>
    public static CidadeReferenciaDto? ParaCidadeDto(
        string? codigoIbge,
        string? nome,
        string? uf,
        string? origem,
        DateTimeOffset? displayAtualizadoEm)
    {
        if (string.IsNullOrWhiteSpace(codigoIbge) || nome is null || uf is null)
        {
            return null;
        }

        return new CidadeReferenciaDto(codigoIbge, nome, uf)
        {
            Origem = origem,
            DisplayAtualizadoEm = displayAtualizadoEm,
        };
    }

    /// <summary>Endereço estruturado: nulo quando ausente; com cidade aninhada quando presente.</summary>
    public static EnderecoGeoDto? ToDto(this ReferenciaEnderecoGeo? endereco)
    {
        if (endereco is null)
        {
            return null;
        }

        return new EnderecoGeoDto(
            endereco.Cep,
            endereco.Logradouro,
            endereco.Numero,
            endereco.Complemento,
            endereco.Bairro,
            endereco.Distrito,
            new CidadeReferenciaDto(endereco.CidadeCodigoIbge, endereco.CidadeNome, endereco.CidadeUf),
            endereco.Latitude,
            endereco.Longitude,
            endereco.NivelResolucao,
            endereco.Origem,
            endereco.DisplayAtualizadoEm);
    }
}
