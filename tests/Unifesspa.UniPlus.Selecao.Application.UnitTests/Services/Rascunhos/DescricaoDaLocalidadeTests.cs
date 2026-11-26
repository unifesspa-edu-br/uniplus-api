namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

/// <summary>
/// O nome e a UF do município saem ao lado do código IBGE, com uma só consulta ao Geo; sem resposta do Geo sobre o município,
/// a exportação segue sem os dois, e a consulta nunca roda para o que não é código de município.
/// </summary>
public sealed class DescricaoDaLocalidadeTests
{
    private const string CodigoDeMaraba = "1504208";

    private readonly IConferenciaEnderecoGeo _geo = Substitute.For<IConferenciaEnderecoGeo>();

    private Task Descrever(JsonObject documento) =>
        new DescricaoDaLocalidade(_geo).DescreverAsync(documento, CancellationToken.None);

    private void OGeoRespondeComOMunicipio() =>
        _geo.ConferirMunicipioAsync(CodigoDeMaraba, "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));

    private void OGeoRespondeComAFalha(string codigoDoErro) =>
        _geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError(codigoDoErro, "O Geo não respondeu.")));

    private static JsonObject DocumentoComLocalidade(string codigo) => JsonNode.Parse(
        $$"""{ "identificacao": { "nome": "Processo Seletivo de Exemplo", "localidadeCodigoIbge": "{{codigo}}", "identificadorLegivel": "psr-2028" } }""")!.AsObject();

    [Fact(DisplayName = "O nome e a UF do município entram logo depois do código IBGE, numa só consulta ao Geo")]
    public async Task Descrever_MunicipioConferido_EscreveNomeEUfAoLadoDoCodigo()
    {
        OGeoRespondeComOMunicipio();
        JsonObject documento = DocumentoComLocalidade(CodigoDeMaraba);

        await Descrever(documento);

        documento["identificacao"]!.AsObject().Select(static propriedade => propriedade.Key).Should().Equal(
            "nome", "localidadeCodigoIbge", "localidadeNome", "localidadeUf", "identificadorLegivel");
        documento["identificacao"]!["localidadeNome"]!.GetValue<string>().Should().Be("Marabá");
        documento["identificacao"]!["localidadeUf"]!.GetValue<string>().Should().Be("PA");
        await _geo.Received(1).ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "O Geo fora do ar ou sem o município omite o nome e a UF e deixa o resto do documento como está")]
    [InlineData(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel)]
    [InlineData(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado)]
    [InlineData(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente)]
    public async Task Descrever_GeoSemOMunicipio_OmiteNomeEUf(string codigoDoErro)
    {
        OGeoRespondeComAFalha(codigoDoErro);
        JsonObject documento = DocumentoComLocalidade(CodigoDeMaraba);

        await Descrever(documento);

        documento["identificacao"]!.AsObject().Select(static propriedade => propriedade.Key).Should().Equal(
            "nome", "localidadeCodigoIbge", "identificadorLegivel");
    }

    [Fact(DisplayName = "O nome que o domínio não aceita como município é omitido junto com a UF")]
    public async Task Descrever_NomeDoGeoInvalido_OmiteNomeEUf()
    {
        _geo.ConferirMunicipioAsync(CodigoDeMaraba, "PA", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(new string('x', 151)));
        JsonObject documento = DocumentoComLocalidade(CodigoDeMaraba);

        await Descrever(documento);

        documento["identificacao"]!["localidadeNome"].Should().BeNull();
        documento["identificacao"]!["localidadeUf"].Should().BeNull();
    }

    [Theory(DisplayName = "O que não tem a forma de código de município não chega ao Geo")]
    [InlineData("123")]
    [InlineData("9904208")]
    public async Task Descrever_CodigoForaDaForma_NaoConsultaOGeo(string codigo)
    {
        JsonObject documento = DocumentoComLocalidade(codigo);

        await Descrever(documento);

        await _geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
        documento["identificacao"]!["localidadeNome"].Should().BeNull();
    }

    [Theory(DisplayName = "Documento sem identificação ou sem localidade não consulta o Geo")]
    [InlineData("""{ "formato": "x" }""")]
    [InlineData("""{ "identificacao": { "nome": "Processo Seletivo de Exemplo" } }""")]
    [InlineData("""{ "identificacao": { "localidadeCodigoIbge": 1504208 } }""")]
    public async Task Descrever_SemLocalidade_NaoConsultaOGeo(string texto)
    {
        JsonObject documento = JsonNode.Parse(texto)!.AsObject();

        await Descrever(documento);

        await _geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }
}
