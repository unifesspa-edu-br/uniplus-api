namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CampusTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private static ReferenciaEnderecoGeo Endereco(string cidadeCodigoIbge = "1504208", string cidadeUf = "PA") =>
        ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            cidadeCodigoIbge, "Marabá", cidadeUf, -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro", Agora).Value!;

    [Fact(DisplayName = "Criar com dados válidos persiste a referência de cidade e o display cache")]
    public void Criar_DadosValidos_PreencheReferenciaCidade()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, "12345");

        resultado.IsSuccess.Should().BeTrue();
        Campus campus = resultado.Value!;
        campus.Sigla.Should().Be("CAMAR", "a sigla é normalizada para uppercase");
        campus.Nome.Should().Be("Campus Marabá");
        campus.CidadeCodigoIbge.Should().Be("1504208");
        campus.CidadeNome.Should().Be("Marabá");
        campus.CidadeUf.Should().Be("PA");
        campus.CidadeOrigem.Should().Be("geo-api");
        campus.CidadeDisplayAtualizadoEm.Should().Be(Agora);
        campus.Endereco.Should().BeNull("nenhum endereço estruturado foi informado");
    }

    [Fact(DisplayName = "Criar com endereço estruturado coerente persiste o endereço")]
    public void Criar_ComEnderecoCoerente_PersisteEndereco()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, Endereco(), null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Endereco!.Cep.Should().Be("68507590");
        resultado.Value!.Endereco!.CidadeCodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "Criar com endereço de cidade incoerente com a cidade do campus falha (CA-04)")]
    public void Criar_EnderecoCidadeIncoerente_Falha()
    {
        // Endereço resolvido em Belém (1501402) num campus de Marabá (1504208).
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, Endereco(cidadeCodigoIbge: "1501402"), null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeIncoerente);
    }

    [Fact(DisplayName = "Criar com código IBGE malformado falha com erro de formato de cidade")]
    public void Criar_CodigoIbgeMalformado_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "150420", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Criar com UF incoerente com o prefixo do código falha")]
    public void Criar_UfIncoerente_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "SP",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Criar sem sigla falha")]
    public void Criar_SemSigla_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "  ", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
    }

    [Fact(DisplayName = "Atualizar troca os campos e mantém validação")]
    public void Atualizar_DadosValidos_Aplica()
    {
        Campus campus = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        Result resultado = campus.Atualizar(
            "CABel", "Campus Belém", "1501402", "Belém", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsSuccess.Should().BeTrue();
        campus.Sigla.Should().Be("CABEL");
        campus.CidadeCodigoIbge.Should().Be("1501402");
        campus.CidadeNome.Should().Be("Belém");
    }
}
