namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ValorDeMunicipioTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Theory(DisplayName = "O município só é lido como código em texto ou como objeto com o código e o nome, nada mais")]
    [InlineData("\"2927408\"", "2927408", null)]
    [InlineData("{\"codigo\":\"2927408\",\"rotulo\":\"Salvador\"}", "2927408", "Salvador")]
    public void TentarLer_FormasValidas(string json, string codigo, string? rotulo)
    {
        ValorDeMunicipio.TentarLer(Json(json), out string lido, out string? nome).Should().BeTrue();

        lido.Should().Be(codigo);
        nome.Should().Be(rotulo);
    }

    [Theory(DisplayName = "O município com forma fora das duas é recusado")]
    [InlineData("\"\"")]
    [InlineData("2927408")]
    [InlineData("{\"codigo\":\"2927408\"}")]
    [InlineData("{\"codigo\":\"2927408\",\"rotulo\":\" \"}")]
    [InlineData("{\"codigo\":\"2927408\",\"rotulo\":\"Salvador\",\"uf\":\"BA\"}")]
    [InlineData("{\"codigo\":2927408,\"rotulo\":\"Salvador\"}")]
    public void TentarLer_FormasInvalidas(string json)
    {
        ValorDeMunicipio.TentarLer(Json(json), out _, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Para as regras o município gravado com nome vira o código, e o que não é município fica como está")]
    public void ComoCodigo_ReduzOObjetoGravado()
    {
        ValorDeMunicipio.ComoCodigo(ValorDeMunicipio.Gravado("2927408", "Salvador")).GetString().Should().Be("2927408");
        ValorDeMunicipio.ComoCodigo(Json("\"2927408\"")).GetString().Should().Be("2927408");
        ValorDeMunicipio.ComoCodigo(Json("{\"cep\":\"40000000\"}")).GetRawText().Should().Be("{\"cep\":\"40000000\"}");
    }

    [Fact(DisplayName = "A entrada da avaliação entrega às regras o município pelo código nas respostas, nos grupos e nos fatos conhecidos")]
    public void Montar_EntregaOMunicipioPeloCodigo()
    {
        JsonElement gravado = ValorDeMunicipio.Gravado("2927408", "Salvador");
        JsonElement endereco = Json("{\"cep\":\"40000000\"}");
        Dictionary<string, DefinicaoDaResposta> definicoes = new(StringComparer.Ordinal)
        {
            ["NATURALIDADE_MUNICIPIO"] = new(TipoDoValorDeResposta.Municipio, Multivalorado: false, Identificador: false),
            ["MUNICIPIO_RESIDENCIA"] = new(TipoDoValorDeResposta.Municipio, Multivalorado: false, Identificador: false),
            ["ENDERECO_RESIDENCIAL"] = new(TipoDoValorDeResposta.Endereco, Multivalorado: false, Identificador: false),
        };

        EntradaDaAvaliacaoDaInscricao entrada = EntradaDaAvaliacaoDaInscricao.Montar(
            definicoes,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = gravado, ["ENDERECO_RESIDENCIAL"] = endereco },
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new OcorrenciaRespondida("local-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = gravado })],
            },
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)
            {
                ["MUNICIPIO_RESIDENCIA"] = FatoResolvido.Resolvido(gravado),
                ["UF_RESIDENCIA"] = FatoResolvido.Indeterminado(),
            },
            new HashSet<string>(StringComparer.Ordinal),
            envio: null,
            EntradaDaAvaliacaoDaInscricao.SemApresentacoes);

        entrada.Respostas["NATURALIDADE_MUNICIPIO"].GetString().Should().Be("2927408");
        entrada.Respostas["ENDERECO_RESIDENCIAL"].GetRawText().Should().Be(endereco.GetRawText());
        entrada.Grupos["LOCAIS"].Single().Respostas["NATURALIDADE_MUNICIPIO"].GetString().Should().Be("2927408");
        entrada.FatosConhecidos["MUNICIPIO_RESIDENCIA"].Valor!.Value.GetString().Should().Be("2927408");
        entrada.FatosConhecidos["UF_RESIDENCIA"].Estado.Should().Be(EstadoFato.Indeterminado);
    }
}
