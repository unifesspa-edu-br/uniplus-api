namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Enderecos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// CA-07 (#726): testes do value object <see cref="ReferenciaEnderecoGeo"/> —
/// validação de formato (referência fraca, sem consultar o Geo), tolerância a
/// resolução parcial, normalização, coerência cidade↔CEP e comparação de conteúdo
/// para re-carimbo do display cache.
/// </summary>
public sealed class ReferenciaEnderecoGeoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 22, 17, 10, 0, TimeSpan.Zero);

    private const string Cep = "68507590";
    private const string Logradouro = "Folha 31, Quadra 7";
    private const string Numero = "s/n";
    private const string Complemento = "Bloco A";
    private const string Bairro = "Nova Marabá";
    private const string CidadeCodigoIbge = "1504208";
    private const string CidadeNome = "Marabá";
    private const string CidadeUf = "PA";
    private const decimal Latitude = -5.368m;
    private const decimal Longitude = -49.118m;
    private const string Origem = "logradouro";

    [Fact(DisplayName = "Criar com dados completos válidos preenche e normaliza o endereço")]
    public void Criar_DadosValidos_NormalizaCampos()
    {
        Result<ReferenciaEnderecoGeo> resultado = ReferenciaEnderecoGeo.Criar(
            " 68507590 ", Logradouro, Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, "pa", Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora);

        resultado.IsSuccess.Should().BeTrue();
        ReferenciaEnderecoGeo endereco = resultado.Value!;
        endereco.Cep.Should().Be("68507590");
        endereco.Logradouro.Should().Be("Folha 31, Quadra 7");
        endereco.CidadeCodigoIbge.Should().Be("1504208");
        endereco.CidadeUf.Should().Be("PA", "a UF é normalizada para caixa alta");
        endereco.NivelResolucao.Should().Be(NivelResolucaoEndereco.Logradouro);
        endereco.Origem.Should().Be("logradouro");
        endereco.DisplayAtualizadoEm.Should().Be(Agora);
    }

    [Fact(DisplayName = "Criar tolera resolução parcial (só cidade): logradouro/bairro/coordenada nulos")]
    public void Criar_ResolucaoParcial_Aceita()
    {
        Result<ReferenciaEnderecoGeo> resultado = ReferenciaEnderecoGeo.Criar(
            Cep, null, null, null, null, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, null, null,
            NivelResolucaoEndereco.Cidade, "faixa-cidade", Agora);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Logradouro.Should().BeNull();
        resultado.Value!.NivelResolucao.Should().Be(NivelResolucaoEndereco.Cidade);
    }

    [Theory(DisplayName = "Criar sem CEP ou com CEP malformado falha")]
    [InlineData(null, EnderecoReferenciaErrorCodes.CepObrigatorio)]
    [InlineData("   ", EnderecoReferenciaErrorCodes.CepObrigatorio)]
    [InlineData("6850759", EnderecoReferenciaErrorCodes.CepFormatoInvalido)]
    [InlineData("68507-590", EnderecoReferenciaErrorCodes.CepFormatoInvalido)]
    [InlineData("6850759X", EnderecoReferenciaErrorCodes.CepFormatoInvalido)]
    public void Criar_CepInvalido_Falha(string? cep, string esperado)
    {
        Result<ReferenciaEnderecoGeo> resultado = ReferenciaEnderecoGeo.Criar(
            cep, Logradouro, Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar com código IBGE do snapshot malformado falha com erro de cidade")]
    public void Criar_CidadeSnapshotMalformado_Falha()
    {
        Result<ReferenciaEnderecoGeo> resultado = ReferenciaEnderecoGeo.Criar(
            Cep, Logradouro, Numero, Complemento, Bairro, null,
            "150420", CidadeNome, CidadeUf, Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Theory(DisplayName = "Criar com nível de resolução ausente ou fora do vocabulário falha")]
    [InlineData(null, EnderecoReferenciaErrorCodes.NivelResolucaoObrigatorio)]
    [InlineData("quadra", EnderecoReferenciaErrorCodes.NivelResolucaoInvalido)]
    public void Criar_NivelResolucaoInvalido_Falha(string? nivel, string esperado)
    {
        Result<ReferenciaEnderecoGeo> resultado = ReferenciaEnderecoGeo.Criar(
            Cep, Logradouro, Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, Latitude, Longitude,
            nivel, Origem, Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar sem origem falha")]
    public void Criar_SemOrigem_Falha()
    {
        Result<ReferenciaEnderecoGeo> resultado = ReferenciaEnderecoGeo.Criar(
            Cep, Logradouro, Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, "  ", Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.OrigemObrigatoria);
    }

    [Theory(DisplayName = "Criar com coordenada fora de faixa falha")]
    [InlineData(-91, 0, EnderecoReferenciaErrorCodes.LatitudeForaDeFaixa)]
    [InlineData(0, 181, EnderecoReferenciaErrorCodes.LongitudeForaDeFaixa)]
    public void Criar_CoordenadaForaDeFaixa_Falha(double latitude, double longitude, string esperado)
    {
        Result<ReferenciaEnderecoGeo> resultado = ReferenciaEnderecoGeo.Criar(
            Cep, Logradouro, Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, (decimal)latitude, (decimal)longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar com logradouro acima do limite falha")]
    public void Criar_LogradouroLongo_Falha()
    {
        Result<ReferenciaEnderecoGeo> resultado = ReferenciaEnderecoGeo.Criar(
            Cep, new string('x', ReferenciaEnderecoGeo.LogradouroMaxLength + 1), Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.LogradouroTamanho);
    }

    [Fact(DisplayName = "EhValido espelha o sucesso de Criar")]
    public void EhValido_Coerente_True()
    {
        ReferenciaEnderecoGeo.EhValido(
            "68507590", null, null, null, null, null,
            "1504208", "Marabá", "PA", null, null, NivelResolucaoEndereco.Cidade, "faixa-cidade")
            .Should().BeTrue();

        ReferenciaEnderecoGeo.EhValido(
            "abc", null, null, null, null, null,
            "1504208", "Marabá", "PA", null, null, NivelResolucaoEndereco.Cidade, "faixa-cidade")
            .Should().BeFalse();
    }

    [Fact(DisplayName = "ValidarCoerencia aceita quando código IBGE e UF coincidem")]
    public void ValidarCoerencia_Coincide_Sucesso()
    {
        Result resultado = ReferenciaEnderecoGeo.ValidarCoerencia("1504208", "PA", "1504208", "pa");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "ValidarCoerencia rejeita quando código ou UF divergem")]
    [InlineData("1501402", "PA")]   // código diferente
    [InlineData("1504208", "SP")]   // UF diferente
    public void ValidarCoerencia_Diverge_Falha(string enderecoCodigo, string enderecoUf)
    {
        Result resultado = ReferenciaEnderecoGeo.ValidarCoerencia(enderecoCodigo, enderecoUf, "1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeIncoerente);
    }

    [Fact(DisplayName = "ValidarCoerencia é vácua quando um dos lados está ausente")]
    public void ValidarCoerencia_LadoAusente_Sucesso()
    {
        ReferenciaEnderecoGeo.ValidarCoerencia(null, null, "1504208", "PA").IsSuccess.Should().BeTrue();
        ReferenciaEnderecoGeo.ValidarCoerencia("1504208", "PA", null, null).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "ConteudoEquivale ignora o display cache (mesmo conteúdo, carimbo distinto)")]
    public void ConteudoEquivale_MesmoConteudoOutroCarimbo_True()
    {
        ReferenciaEnderecoGeo a = ReferenciaEnderecoGeo.Criar(
            Cep, Logradouro, Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora).Value!;
        ReferenciaEnderecoGeo b = ReferenciaEnderecoGeo.Criar(
            Cep, Logradouro, Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora.AddDays(30)).Value!;

        a.ConteudoEquivale(b).Should().BeTrue();
    }

    [Fact(DisplayName = "ConteudoEquivale detecta diferença de conteúdo")]
    public void ConteudoEquivale_ConteudoDiferente_False()
    {
        ReferenciaEnderecoGeo a = ReferenciaEnderecoGeo.Criar(
            Cep, Logradouro, Numero, Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora).Value!;
        ReferenciaEnderecoGeo b = ReferenciaEnderecoGeo.Criar(
            Cep, Logradouro, "100", Complemento, Bairro, null,
            CidadeCodigoIbge, CidadeNome, CidadeUf, Latitude, Longitude,
            NivelResolucaoEndereco.Logradouro, Origem, Agora).Value!;

        a.ConteudoEquivale(b).Should().BeFalse();
        a.ConteudoEquivale(null).Should().BeFalse();
    }
}
