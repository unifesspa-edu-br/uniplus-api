namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CampusTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Criar com dados válidos persiste a referência de cidade e o display cache")]
    public void Criar_DadosValidos_PreencheReferenciaCidade()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, "Folha 31", "68507590", -5.3m, -49.1m, "12345");

        resultado.IsSuccess.Should().BeTrue();
        Campus campus = resultado.Value!;
        campus.Sigla.Should().Be("CAMAR", "a sigla é normalizada para uppercase");
        campus.Nome.Should().Be("Campus Marabá");
        campus.CidadeCodigoIbge.Should().Be("1504208");
        campus.CidadeNome.Should().Be("Marabá");
        campus.CidadeUf.Should().Be("PA");
        campus.CidadeOrigem.Should().Be("geo-api");
        campus.CidadeDisplayAtualizadoEm.Should().Be(Agora);
        campus.Cep.Should().Be("68507590");
    }

    [Fact(DisplayName = "Criar com código IBGE malformado falha com erro de formato de cidade")]
    public void Criar_CodigoIbgeMalformado_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "150420", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Criar com UF incoerente com o prefixo do código falha")]
    public void Criar_UfIncoerente_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "SP",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Criar sem sigla falha")]
    public void Criar_SemSigla_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "  ", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
    }

    [Fact(DisplayName = "Criar com CEP em formato inválido falha")]
    public void Criar_CepInvalido_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, "6850-759", null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.CepInvalido);
    }

    [Theory(DisplayName = "Criar com coordenada fora de faixa falha")]
    [InlineData(-91, 0, CampusErrorCodes.LatitudeForaDeFaixa)]
    [InlineData(0, 181, CampusErrorCodes.LongitudeForaDeFaixa)]
    public void Criar_CoordenadaForaDeFaixa_Falha(double latitude, double longitude, string esperado)
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null, (decimal)latitude, (decimal)longitude, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(esperado);
    }

    [Fact(DisplayName = "Atualizar troca os campos e mantém validação")]
    public void Atualizar_DadosValidos_Aplica()
    {
        Campus campus = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null, null, null, null).Value!;

        Result resultado = campus.Atualizar(
            "CABel", "Campus Belém", "1501402", "Belém", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null, null, null, null);

        resultado.IsSuccess.Should().BeTrue();
        campus.Sigla.Should().Be("CABEL");
        campus.CidadeCodigoIbge.Should().Be("1501402");
        campus.CidadeNome.Should().Be("Belém");
    }
}
