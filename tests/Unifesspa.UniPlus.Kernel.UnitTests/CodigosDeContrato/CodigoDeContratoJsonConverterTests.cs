namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class CodigoDeContratoJsonConverterTests
{
    private static readonly JsonSerializerOptions Opcoes =
        OpcoesJsonDeDocumento.Criar(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact(DisplayName = "O enum como propriedade sai pelo código e volta igual")]
    public void IdaEVolta_ComoPropriedade_UsaOCodigo()
    {
        ComCorEscolhida original = new(Cor.VerdeClaro);

        string json = JsonSerializer.Serialize(original, Opcoes);

        json.Should().Be("""{"escolhida":"VERDE_CLARO"}""");
        JsonSerializer.Deserialize<ComCorEscolhida>(json, Opcoes).Should().Be(original);
    }

    [Fact(DisplayName = "O enum como elemento de lista sai pelo código e volta igual")]
    public void IdaEVolta_ComoElementoDeLista_UsaOCodigo()
    {
        List<Cor> original = [Cor.VerdeClaro, Cor.Azul];

        string json = JsonSerializer.Serialize(original, Opcoes);

        json.Should().Be("""["VERDE_CLARO","AZUL"]""");
        JsonSerializer.Deserialize<List<Cor>>(json, Opcoes).Should().Equal(original);
    }

    [Fact(DisplayName = "O enum como chave de dicionário sai pelo código e volta igual")]
    public void IdaEVolta_ComoChaveDeDicionario_UsaOCodigo()
    {
        Dictionary<Cor, int> original = new() { [Cor.VerdeClaro] = 2, [Cor.Azul] = 1 };

        string json = JsonSerializer.Serialize(original, Opcoes);

        json.Should().Be("""{"VERDE_CLARO":2,"AZUL":1}""");
        JsonSerializer.Deserialize<Dictionary<Cor, int>>(json, Opcoes).Should().Equal(original);
    }

    [Fact(DisplayName = "A escrita usa o código declarado, e não o nome do membro")]
    public void Escrever_UsaOCodigoDeclarado_NaoONomeDoMembro() =>
        JsonSerializer.Serialize(Cor.VerdeClaro, Opcoes).Should().Be("\"VERDE_CLARO\"").And.NotContain(nameof(Cor.VerdeClaro));

    [Theory(DisplayName = "Código desconhecido ou token que não é texto lançam JsonException que cita o enum")]
    [InlineData("\"roxo\"")]
    [InlineData("\"Azul\"")]
    [InlineData("\"\"")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Ler_CodigoInvalido_LancaJsonException(string json) =>
        FluentActions.Invoking(() => JsonSerializer.Deserialize<Cor>(json, Opcoes))
            .Should().Throw<JsonException>().Which.Message.Should().Contain(nameof(Cor));

    [Theory(DisplayName = "A recusa de código desconhecido não repete o valor recebido")]
    [InlineData("valor_recebido")]
    [InlineData("Azul")]
    public void Ler_CodigoDesconhecido_NaoRepeteOValorRecebido(string codigo) =>
        FluentActions.Invoking(() => JsonSerializer.Deserialize<ComCorEscolhida>($$"""{"escolhida":"{{codigo}}"}""", Opcoes))
            .Should().Throw<JsonException>().Which.Message.Should().NotContain(codigo);

    [Fact(DisplayName = "Código desconhecido como chave de dicionário lança JsonException")]
    public void Ler_CodigoDesconhecidoComoChave_LancaJsonException() =>
        FluentActions.Invoking(() => JsonSerializer.Deserialize<Dictionary<Cor, int>>("""{"roxo":1}""", Opcoes))
            .Should().Throw<JsonException>().Which.Message.Should().Contain(nameof(Cor));

    [Theory(DisplayName = "O enum anulável usa o mesmo conversor, nulo ou preenchido, como propriedade")]
    [InlineData(null, """{"opcional":null}""")]
    [InlineData(Cor.Azul, """{"opcional":"AZUL"}""")]
    public void IdaEVolta_EnumAnulavel_UsaOMesmoConversor(Cor? valor, string jsonEsperado)
    {
        ComCorOpcional original = new(valor);

        string json = JsonSerializer.Serialize(original, Opcoes);

        json.Should().Be(jsonEsperado);
        JsonSerializer.Deserialize<ComCorOpcional>(json, Opcoes).Should().Be(original);
    }

    [Fact(DisplayName = "O enum inválido na leitura derruba com a exceção do catálogo, e não com JsonException")]
    public void Ler_EnumInvalido_LancaAExcecaoDoCatalogo() =>
        FluentActions.Invoking(() => JsonSerializer.Deserialize<ComCodigoRepetido>("\"igual\"", Opcoes))
            .Should().Throw<CodigosDeContratoInvalidosException>();

    [Fact(DisplayName = "O valor fora do enum não é escrito")]
    public void Escrever_ValorForaDoEnum_Lanca() =>
        FluentActions.Invoking(() => JsonSerializer.Serialize((Cor)99, Opcoes)).Should().Throw<ArgumentOutOfRangeException>();

    private sealed record ComCorEscolhida(Cor Escolhida);

    private sealed record ComCorOpcional(Cor? Opcional);
}
