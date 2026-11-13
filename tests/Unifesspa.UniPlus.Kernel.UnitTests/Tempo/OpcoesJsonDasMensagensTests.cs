namespace Unifesspa.UniPlus.Kernel.UnitTests.Tempo;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;

public sealed class OpcoesJsonDasMensagensTests
{
    private static readonly JsonSerializerOptions Opcoes = Criar();

    private static readonly DateTimeOffset Abertura = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "O instante não anulável ausente é recusado com JsonException que nomeia a propriedade")]
    public void Ler_InstanteNaoAnulavelAusente_LancaJsonException()
    {
        Action ler = () => JsonSerializer.Deserialize<Evento>("{\"nome\":\"abertura\"}", Opcoes);

        ler.Should().Throw<JsonException>().WithMessage("*occurredOn*");
    }

    [Fact(DisplayName = "O instante não anulável presente é lido truncado em microssegundos")]
    public void Ler_InstanteNaoAnulavelPresente_TruncaEmMicrossegundos()
    {
        Evento? lido = JsonSerializer.Deserialize<Evento>(
            "{\"nome\":\"abertura\",\"occurredOn\":\"2026-11-12T10:00:00.1234567Z\"}", Opcoes);

        lido!.OccurredOn.Should().Be(Abertura.AddTicks(1_234_560));
    }

    [Fact(DisplayName = "O instante anulável ausente continua opcional e o presente é truncado")]
    public void Ler_InstanteAnulavel_ContinuaOpcional()
    {
        ComPrazoOpcional? ausente = JsonSerializer.Deserialize<ComPrazoOpcional>("{\"nome\":\"a\"}", Opcoes);
        ComPrazoOpcional? presente = JsonSerializer.Deserialize<ComPrazoOpcional>(
            "{\"nome\":\"a\",\"prazo\":\"2026-11-12T10:00:00.9999999Z\"}", Opcoes);

        ausente!.Prazo.Should().BeNull();
        presente!.Prazo.Should().Be(Abertura.AddTicks(9_999_990));
    }

    [Fact(DisplayName = "O instante não anulável de objeto aninhado é obrigatório")]
    public void Ler_InstanteNaoAnulavelAninhadoAusente_LancaJsonException()
    {
        Action ler = () => JsonSerializer.Deserialize<ComEventoAninhado>("{\"evento\":{\"nome\":\"abertura\"}}", Opcoes);

        ler.Should().Throw<JsonException>().WithMessage("*occurredOn*");
    }

    [Fact(DisplayName = "O instante não anulável de cada item de uma coleção é obrigatório")]
    public void Ler_InstanteNaoAnulavelDeItemDeColecaoAusente_LancaJsonException()
    {
        const string json = "{\"eventos\":[{\"nome\":\"a\",\"occurredOn\":\"2026-11-12T10:00:00Z\"},{\"nome\":\"b\"}]}";

        Action ler = () => JsonSerializer.Deserialize<ComColecaoDeEventos>(json, Opcoes);

        ler.Should().Throw<JsonException>().WithMessage("*occurredOn*");
    }

    [Fact(DisplayName = "Os instantes dos itens de uma coleção presentes são lidos truncados")]
    public void Ler_ColecaoComInstantes_TruncaCadaItem()
    {
        const string json = "{\"eventos\":[{\"nome\":\"a\",\"occurredOn\":\"2026-11-12T10:00:00.0000019Z\"}]}";

        ComColecaoDeEventos? lido = JsonSerializer.Deserialize<ComColecaoDeEventos>(json, Opcoes);

        lido!.Eventos.Should().ContainSingle().Which.OccurredOn.Should().Be(Abertura.AddTicks(10));
    }

    [Fact(DisplayName = "O instante definido em propriedade com set é obrigatório")]
    public void Ler_InstanteEmPropriedadeComSetAusente_LancaJsonException()
    {
        Action ler = () => JsonSerializer.Deserialize<ComPropriedadeMutavel>("{}", Opcoes);

        ler.Should().Throw<JsonException>().WithMessage("*quando*");
    }

    [Fact(DisplayName = "O instante calculado, sem como ser lido, não vira obrigatório nem impede a escrita")]
    public void Serializar_InstanteCalculado_NaoImpedeAEscritaNemALeitura()
    {
        ComInstanteCalculado original = new("a");

        string json = JsonSerializer.Serialize(original, Opcoes);
        ComInstanteCalculado? lido = JsonSerializer.Deserialize<ComInstanteCalculado>(json, Opcoes);

        lido!.Nome.Should().Be("a");
    }

    [Fact(DisplayName = "O instante só de leitura preenchido pelo construtor continua lido, truncado, sem exigência do serializador")]
    public void Ler_InstanteDoConstrutor_LidoTruncado()
    {
        ComInstanteDoConstrutor? lido = JsonSerializer.Deserialize<ComInstanteDoConstrutor>(
            "{\"nome\":\"a\",\"quando\":\"2026-11-12T10:00:00.1234567Z\"}", Opcoes);

        lido!.Quando.Should().Be(Abertura.AddTicks(1_234_560));
    }

    [Fact(DisplayName = "A mensagem escrita com o contrato volta íntegra na leitura")]
    public void IdaEVolta_PreservaOEventoComInstanteEmMicrossegundos()
    {
        Evento original = new("abertura", Abertura.AddTicks(1_234_560));

        Evento? lido = JsonSerializer.Deserialize<Evento>(JsonSerializer.Serialize(original, Opcoes), Opcoes);

        lido.Should().Be(original);
    }

    [Fact(DisplayName = "Aplicar preserva o resolvedor que as opções já tinham")]
    public void Aplicar_PreservaOResolvedorExistente()
    {
        JsonSerializerOptions opcoes = new() { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

        OpcoesJsonDasMensagens.Aplicar(opcoes);

        Action ler = () => JsonSerializer.Deserialize<Evento>("{\"nome\":\"a\"}", opcoes);
        ler.Should().Throw<JsonException>();
    }

    private static JsonSerializerOptions Criar()
    {
        JsonSerializerOptions opcoes = new(JsonSerializerDefaults.Web);
        OpcoesJsonDasMensagens.Aplicar(opcoes);
        return opcoes;
    }

    private sealed record Evento(string Nome, DateTimeOffset OccurredOn);

    private sealed record ComPrazoOpcional(string Nome, DateTimeOffset? Prazo);

    private sealed record ComEventoAninhado(Evento Evento);

    private sealed record ComColecaoDeEventos(IReadOnlyList<Evento> Eventos);

    private sealed class ComPropriedadeMutavel
    {
        public DateTimeOffset Quando { get; set; }
    }

    private sealed class ComInstanteDoConstrutor
    {
        public ComInstanteDoConstrutor(string nome, DateTimeOffset quando)
        {
            Nome = nome;
            Quando = quando;
        }

        public string Nome { get; }

        public DateTimeOffset Quando { get; }
    }

    private sealed class ComInstanteCalculado
    {
        public ComInstanteCalculado(string nome) => Nome = nome;

        public string Nome { get; }

        public DateTimeOffset Gerado => DateTimeOffset.UnixEpoch.AddDays(Nome.Length);
    }
}
