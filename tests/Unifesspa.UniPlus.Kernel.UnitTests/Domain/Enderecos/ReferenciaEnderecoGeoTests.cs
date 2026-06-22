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

    private static Result<ReferenciaEnderecoGeo> CriarValido(
        string? cep = "68507590",
        string? logradouro = "Folha 31, Quadra 7",
        string? numero = "s/n",
        string? complemento = "Bloco A",
        string? bairro = "Nova Marabá",
        string? distrito = null,
        string? cidadeCodigoIbge = "1504208",
        string? cidadeNome = "Marabá",
        string? cidadeUf = "PA",
        decimal? latitude = -5.368m,
        decimal? longitude = -49.118m,
        string? nivelResolucao = NivelResolucaoEndereco.Logradouro,
        string? origem = "logradouro") =>
        ReferenciaEnderecoGeo.Criar(
            cep, logradouro, numero, complemento, bairro, distrito,
            cidadeCodigoIbge, cidadeNome, cidadeUf, latitude, longitude, nivelResolucao, origem, Agora);

    [Fact(DisplayName = "Criar com dados completos válidos preenche e normaliza o endereço")]
    public void Criar_DadosValidos_NormalizaCampos()
    {
        Result<ReferenciaEnderecoGeo> resultado = CriarValido(cep: " 68507590 ", cidadeUf: "pa");

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
        Result<ReferenciaEnderecoGeo> resultado = CriarValido(
            logradouro: null, numero: null, complemento: null, bairro: null, distrito: null,
            latitude: null, longitude: null, nivelResolucao: NivelResolucaoEndereco.Cidade, origem: "faixa-cidade");

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
        Result<ReferenciaEnderecoGeo> resultado = CriarValido(cep: cep);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar com código IBGE do snapshot malformado falha com erro de cidade")]
    public void Criar_CidadeSnapshotMalformado_Falha()
    {
        Result<ReferenciaEnderecoGeo> resultado = CriarValido(cidadeCodigoIbge: "150420");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Theory(DisplayName = "Criar com nível de resolução ausente ou fora do vocabulário falha")]
    [InlineData(null, EnderecoReferenciaErrorCodes.NivelResolucaoObrigatorio)]
    [InlineData("quadra", EnderecoReferenciaErrorCodes.NivelResolucaoInvalido)]
    public void Criar_NivelResolucaoInvalido_Falha(string? nivel, string esperado)
    {
        Result<ReferenciaEnderecoGeo> resultado = CriarValido(nivelResolucao: nivel);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar sem origem falha")]
    public void Criar_SemOrigem_Falha()
    {
        Result<ReferenciaEnderecoGeo> resultado = CriarValido(origem: "  ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.OrigemObrigatoria);
    }

    [Theory(DisplayName = "Criar com coordenada fora de faixa falha")]
    [InlineData(-91, 0, EnderecoReferenciaErrorCodes.LatitudeForaDeFaixa)]
    [InlineData(0, 181, EnderecoReferenciaErrorCodes.LongitudeForaDeFaixa)]
    public void Criar_CoordenadaForaDeFaixa_Falha(double latitude, double longitude, string esperado)
    {
        Result<ReferenciaEnderecoGeo> resultado = CriarValido(latitude: (decimal)latitude, longitude: (decimal)longitude);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar com logradouro acima do limite falha")]
    public void Criar_LogradouroLongo_Falha()
    {
        Result<ReferenciaEnderecoGeo> resultado = CriarValido(
            logradouro: new string('x', ReferenciaEnderecoGeo.LogradouroMaxLength + 1));

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
        ReferenciaEnderecoGeo a = CriarValido().Value!;
        ReferenciaEnderecoGeo b = ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31, Quadra 7", "s/n", "Bloco A", "Nova Marabá", null,
            "1504208", "Marabá", "PA", -5.368m, -49.118m, NivelResolucaoEndereco.Logradouro, "logradouro",
            Agora.AddDays(30)).Value!;

        a.ConteudoEquivale(b).Should().BeTrue();
    }

    [Fact(DisplayName = "ConteudoEquivale detecta diferença de conteúdo")]
    public void ConteudoEquivale_ConteudoDiferente_False()
    {
        ReferenciaEnderecoGeo a = CriarValido().Value!;
        ReferenciaEnderecoGeo b = CriarValido(numero: "100").Value!;

        a.ConteudoEquivale(b).Should().BeFalse();
        a.ConteudoEquivale(null).Should().BeFalse();
    }
}
