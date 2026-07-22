namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class LocalOfertaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private static ReferenciaEnderecoGeo Endereco(string cidadeCodigoIbge = "1504208", string cidadeUf = "PA") =>
        ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            cidadeCodigoIbge, "Marabá", cidadeUf, -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro", Agora).Value!;

    [Fact(DisplayName = "Criar com dados válidos preenche tipo, campus responsável e referência de cidade")]
    public void Criar_DadosValidos_Preenche()
    {
        Guid campusId = Guid.CreateVersion7();

        Result<LocalOferta> resultado = LocalOferta.Criar(
            TipoLocalOferta.PoloEad, campusId, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, "98765");

        resultado.IsSuccess.Should().BeTrue();
        LocalOferta local = resultado.Value!;
        local.Tipo.Should().Be(TipoLocalOferta.PoloEad);
        local.CampusResponsavelId.Should().Be(campusId);
        local.CidadeCodigoIbge.Should().Be("1504208");
        local.CidadeUf.Should().Be("PA");
        local.CidadeOrigem.Should().Be("geo-api");
        local.Endereco.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com endereço estruturado coerente persiste o endereço")]
    public void Criar_ComEnderecoCoerente_PersisteEndereco()
    {
        Result<LocalOferta> resultado = LocalOferta.Criar(
            TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, Endereco(), null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Endereco!.Cep.Should().Be("68507590");
    }

    [Fact(DisplayName = "Criar com endereço de cidade incoerente falha (CA-04)")]
    public void Criar_EnderecoCidadeIncoerente_Falha()
    {
        Result<LocalOferta> resultado = LocalOferta.Criar(
            TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, Endereco(cidadeCodigoIbge: "1501402"), null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeIncoerente);
    }

    [Fact(DisplayName = "Criar sem campus responsável é válido (FK intra-banco opcional, ADR-0065)")]
    public void Criar_SemCampusResponsavel_Valido()
    {
        Result<LocalOferta> resultado = LocalOferta.Criar(
            TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CampusResponsavelId.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com tipo Nenhum (sentinela) falha")]
    public void Criar_TipoNenhum_Falha()
    {
        Result<LocalOferta> resultado = LocalOferta.Criar(
            TipoLocalOferta.Nenhum, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.TipoInvalido);
    }

    [Fact(DisplayName = "Criar com referência de cidade malformada falha")]
    public void Criar_CidadeMalformada_Falha()
    {
        Result<LocalOferta> resultado = LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "ABCDEFG", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }
}
