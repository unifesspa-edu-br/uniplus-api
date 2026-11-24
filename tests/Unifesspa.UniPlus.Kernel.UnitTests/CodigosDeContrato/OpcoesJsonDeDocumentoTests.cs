namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class OpcoesJsonDeDocumentoTests
{
    [Fact(DisplayName = "O conversor de códigos vence o conversor de enum por nome que estava à frente")]
    public void Adicionar_ConversorDeEnumPorNomeAFrente_EscreveOCodigo()
    {
        JsonSerializerOptions opcoes = new() { Converters = { new JsonStringEnumConverter() } };

        opcoes.AdicionarConversorDeCodigosDeContrato();

        EhAFabricaDeCodigos(opcoes.Converters[0]).Should().BeTrue();
        JsonSerializer.Serialize(Cor.VerdeClaro, opcoes).Should().Be("\"VERDE_CLARO\"");
    }

    [Fact(DisplayName = "A chamada repetida deixa uma só fábrica, na posição 0")]
    public void Adicionar_ChamadaRepetida_DeixaUmaFabricaSoNaPosicaoZero()
    {
        JsonSerializerOptions opcoes = new() { Converters = { new JsonStringEnumConverter() } };

        opcoes.AdicionarConversorDeCodigosDeContrato();
        opcoes.AdicionarConversorDeCodigosDeContrato();

        opcoes.Converters.Should().HaveCount(2);
        opcoes.Converters.Count(EhAFabricaDeCodigos).Should().Be(1);
        EhAFabricaDeCodigos(opcoes.Converters[0]).Should().BeTrue();
    }

    [Fact(DisplayName = "A fábrica que ficou atrás de outro conversor volta à frente sem duplicar")]
    public void Adicionar_FabricaAtrasDeOutroConversor_VoltaAFrenteSemDuplicar()
    {
        JsonSerializerOptions opcoes = new();
        opcoes.AdicionarConversorDeCodigosDeContrato();
        opcoes.Converters.Insert(0, new JsonStringEnumConverter());

        opcoes.AdicionarConversorDeCodigosDeContrato();

        opcoes.Converters.Count(EhAFabricaDeCodigos).Should().Be(1);
        EhAFabricaDeCodigos(opcoes.Converters[0]).Should().BeTrue();
        JsonSerializer.Serialize(Cor.VerdeClaro, opcoes).Should().Be("\"VERDE_CLARO\"");
    }

    [Fact(DisplayName = "O enum sem código segue com o conversor que já o atendia")]
    public void Adicionar_EnumSemCodigo_SegueComOConversorDeNome()
    {
        JsonSerializerOptions opcoes = new() { Converters = { new JsonStringEnumConverter() } };

        opcoes.AdicionarConversorDeCodigosDeContrato();

        JsonSerializer.Serialize(SemCodigoDeContrato.Segundo, opcoes).Should().Be("\"Segundo\"");
    }

    [Fact(DisplayName = "As opções já usadas pelo serializador falham como o serializador falha")]
    public void Adicionar_OpcoesJaUsadas_Lanca()
    {
        JsonSerializerOptions opcoes = new();
        JsonSerializer.Serialize(1, opcoes);

        FluentActions.Invoking(() => opcoes.AdicionarConversorDeCodigosDeContrato()).Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "As opções nulas são recusadas")]
    public void Adicionar_OpcoesNulas_Recusa() =>
        FluentActions.Invoking(static () => OpcoesJsonDeDocumento.AdicionarConversorDeCodigosDeContrato(null!))
            .Should().Throw<ArgumentNullException>();

    [Fact(DisplayName = "Escrito e lido pelo ponto de entrada das opções prontas, o enum usa o código em cada posição")]
    public void IdaEVolta_PeloAdicionar_UsaOCodigoEmCadaPosicaoDoEnum()
    {
        JsonSerializerOptions opcoes = new(JsonSerializerDefaults.Web);
        opcoes.AdicionarConversorDeCodigosDeContrato();
        Documento original = new(Cor.VerdeClaro, null, [Cor.Azul], new() { [Cor.VerdeClaro] = 1 });

        string json = JsonSerializer.Serialize(original, opcoes);

        json.Should().Be("""{"escolhida":"VERDE_CLARO","opcional":null,"lista":["AZUL"],"contagem":{"VERDE_CLARO":1}}""");
        JsonSerializer.Deserialize<Documento>(json, opcoes).Should().BeEquivalentTo(original);
    }

    [Theory(DisplayName = "O código desconhecido, como valor ou como chave, é recusado pelo ponto de entrada das opções prontas")]
    [InlineData("""{"escolhida":"roxo"}""")]
    [InlineData("""{"escolhida":"AZUL","contagem":{"roxo":1}}""")]
    public void Ler_PeloAdicionar_CodigoDesconhecido_LancaJsonException(string json)
    {
        JsonSerializerOptions opcoes = new(JsonSerializerDefaults.Web);
        opcoes.AdicionarConversorDeCodigosDeContrato();

        FluentActions.Invoking(() => JsonSerializer.Deserialize<Documento>(json, opcoes)).Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "As opções criadas preservam as escolhas locais do modelo e escrevem o código do enum")]
    public void Criar_ModeloComEscolhasLocais_ProduzOsBytesEsperados()
    {
        JsonSerializerOptions opcoes = OpcoesJsonDeDocumento.Criar(ModeloComEscolhasLocais());

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Ficha(Cor.VerdeClaro, true, "Açaí & cia", null), opcoes);

        bytes.Should().Equal(Encoding.UTF8.GetBytes("""{"escolhida":"VERDE_CLARO","ativo":"sim","texto":"Açaí & cia"}"""));
    }

    [Fact(DisplayName = "As opções criadas leem de volta o que escreveram")]
    public void Criar_ModeloComEscolhasLocais_IdaEVolta()
    {
        JsonSerializerOptions opcoes = OpcoesJsonDeDocumento.Criar(ModeloComEscolhasLocais());
        Ficha original = new(Cor.Azul, false, "Açaí & cia", "sem nulo");

        Ficha? lida = JsonSerializer.Deserialize<Ficha>(JsonSerializer.Serialize(original, opcoes), opcoes);

        lida.Should().Be(original);
    }

    [Fact(DisplayName = "As opções criadas saem congeladas")]
    public void Criar_Opcoes_SaemCongeladas() =>
        OpcoesJsonDeDocumento.Criar(ModeloComEscolhasLocais()).IsReadOnly.Should().BeTrue();

    [Fact(DisplayName = "O modelo recebido não muda: continua modificável e sem o conversor de códigos")]
    public void Criar_ModeloRecebido_NaoMuda()
    {
        JsonSerializerOptions modelo = ModeloComEscolhasLocais();

        OpcoesJsonDeDocumento.Criar(modelo);

        modelo.IsReadOnly.Should().BeFalse();
        modelo.Converters.Select(static c => c.GetType()).Should().Equal(typeof(JsonStringEnumConverter), typeof(SimOuNaoJsonConverter));
    }

    [Fact(DisplayName = "O modelo já congelado serve de modelo: a cópia nasce modificável e sai congelada com o código")]
    public void Criar_ModeloJaCongelado_CriaCopiaComOCodigo()
    {
        JsonSerializerOptions opcoes = OpcoesJsonDeDocumento.Criar(JsonSerializerOptions.Web);

        opcoes.IsReadOnly.Should().BeTrue();
        JsonSerializer.Serialize(new Ficha(Cor.Azul, true, "a", null), opcoes).Should().Contain("\"escolhida\":\"AZUL\"");
    }

    [Fact(DisplayName = "O enum sem código segue com o conversor de nome do modelo")]
    public void Criar_EnumSemCodigo_SegueComOConversorDeNomeDoModelo() =>
        JsonSerializer.Serialize(SemCodigoDeContrato.Segundo, OpcoesJsonDeDocumento.Criar(ModeloComEscolhasLocais()))
            .Should().Be("\"Segundo\"");

    [Fact(DisplayName = "O modelo nulo é recusado")]
    public void Criar_ModeloNulo_Recusa() =>
        FluentActions.Invoking(static () => OpcoesJsonDeDocumento.Criar(null!)).Should().Throw<ArgumentNullException>();

    private static bool EhAFabricaDeCodigos(JsonConverter conversor) =>
        conversor.CanConvert(typeof(Cor)) && !conversor.CanConvert(typeof(SemCodigoDeContrato));

    private static JsonSerializerOptions ModeloComEscolhasLocais() =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter(), new SimOuNaoJsonConverter() },
        };

    private sealed record Ficha(Cor Escolhida, bool Ativo, string Texto, string? Observacao);

    private sealed record Documento(Cor Escolhida, Cor? Opcional, List<Cor>? Lista, Dictionary<Cor, int>? Contagem);

    private sealed class SimOuNaoJsonConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            string.Equals(reader.GetString(), "sim", StringComparison.Ordinal);

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value ? "sim" : "nao");
    }
}
