namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;

/// <summary>
/// Composição dos sub-objetos aninhados do contrato HTTP (ADR-0096) a partir do
/// estado flat das entidades de Configuração: a referência de cidade do nível
/// raiz (com proveniência do display cache) e o endereço estruturado opcional.
/// </summary>
public static class EnderecoGeoMapping
{
    /// <summary>Cidade do nível raiz: trio + metadados de proveniência/frescura.</summary>
    public static CidadeReferenciaDto ParaCidadeDto(
        string codigoIbge,
        string nome,
        string uf,
        string? origem,
        DateTimeOffset? displayAtualizadoEm) =>
        new(codigoIbge, nome, uf)
        {
            Origem = origem,
            DisplayAtualizadoEm = displayAtualizadoEm,
        };

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
