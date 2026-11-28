namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ImportacaoDoResultadoNoEnvelopeTests
{
    private static readonly Guid Fase = Guid.Parse("018f0000-0000-7000-8000-000000000001");

    private static DeclaracaoDaImportacaoDoResultado Declaracao(Guid? fase) => DeclaracaoDaImportacaoDoResultado.Declarar(
        [new("INEP_RETORNO_ENEM", "v1"), new("INEP_RETORNO_SISU", "v2")], fase).Value!;

    // O envelope é lido dos bytes gravados, em que o identificador é texto: a ida e volta passa pelo perfil canônico.
    private static JsonObject Envelope(DeclaracaoDaImportacaoDoResultado? declaracao) => JsonNode.Parse(
        PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["importacaoDoResultado"] = SerializacaoDaImportacaoDoResultado.Serializar(declaracao) }))!.AsObject();

    [Fact]
    public void Serializar_ComDeclaracao_PreservaOrdemEFase()
    {
        JsonObject bloco = SerializacaoDaImportacaoDoResultado.Serializar(Declaracao(Fase))!;

        bloco["relatorios"]!.AsArray().Select(static r => r!["leiauteCodigo"]!.GetValue<string>())
            .Should().Equal("INEP_RETORNO_ENEM", "INEP_RETORNO_SISU");
        bloco["relatorios"]![1]!["leiauteVersao"]!.GetValue<string>().Should().Be("v2");
        bloco["primeiraFaseDepoisDaImportacaoId"]!.GetValue<Guid>().Should().Be(Fase);
    }

    [Fact]
    public void Serializar_SemDeclaracao_EscreveNulo() =>
        SerializacaoDaImportacaoDoResultado.Serializar(null).Should().BeNull();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ler_BlocoSerializado_ReconstroiDeclaracao(bool comFase)
    {
        DeclaracaoDaImportacaoDoResultado original = Declaracao(comFase ? Fase : null);
        LeitorEnvelope leitor = new();

        DeclaracaoDaImportacaoDoResultado? lida = LeituraDaImportacaoDoResultado.Ler(leitor, Envelope(original));

        leitor.Falhou.Should().BeFalse();
        lida!.PrimeiraFaseDepoisDaImportacaoId.Should().Be(original.PrimeiraFaseDepoisDaImportacaoId);
        lida.Relatorios.Should().Equal(original.Relatorios);
    }

    [Fact]
    public void Ler_BlocoNulo_DeixaSemDeclaracao()
    {
        LeitorEnvelope leitor = new();

        LeituraDaImportacaoDoResultado.Ler(leitor, Envelope(null)).Should().BeNull();

        leitor.Falhou.Should().BeFalse();
    }

    [Theory]
    [InlineData("relatorios")]
    [InlineData("primeiraFaseDepoisDaImportacaoId")]
    public void Ler_ChaveDoBlocoAusente_Recusa(string chave)
    {
        JsonObject envelope = Envelope(Declaracao(Fase));
        envelope["importacaoDoResultado"]!.AsObject().Remove(chave);
        LeitorEnvelope leitor = new();

        LeituraDaImportacaoDoResultado.Ler(leitor, envelope);

        leitor.Falhou.Should().BeTrue();
    }

    [Theory]
    [InlineData("leiauteCodigo")]
    [InlineData("leiauteVersao")]
    public void Ler_ChaveDoRelatorioAusente_Recusa(string chave)
    {
        JsonObject envelope = Envelope(Declaracao(Fase));
        envelope["importacaoDoResultado"]!["relatorios"]![0]!.AsObject().Remove(chave);
        LeitorEnvelope leitor = new();

        LeituraDaImportacaoDoResultado.Ler(leitor, envelope);

        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void Ler_LeiauteRepetido_RecusaPeloDominio()
    {
        JsonObject envelope = Envelope(Declaracao(Fase));
        envelope["importacaoDoResultado"]!["relatorios"]![1]!["leiauteCodigo"] = "INEP_RETORNO_ENEM";
        LeitorEnvelope leitor = new();

        LeituraDaImportacaoDoResultado.Ler(leitor, envelope);

        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void Ler_CodigoAcimaDoLimite_Recusa()
    {
        JsonObject envelope = Envelope(Declaracao(Fase));
        envelope["importacaoDoResultado"]!["relatorios"]![0]!["leiauteCodigo"] = new string('A', RelatorioDeclarado.LeiauteCodigoComprimentoMaximo + 1);
        LeitorEnvelope leitor = new();

        LeituraDaImportacaoDoResultado.Ler(leitor, envelope);

        leitor.Falhou.Should().BeTrue();
    }

    [Theory]
    [InlineData("MEU LEIAUTE")]
    [InlineData("LEIAUTE\u0301")]
    public void Ler_CodigoForaDaFormaDeCatalogo_Recusa(string codigo)
    {
        JsonObject envelope = Envelope(Declaracao(Fase));
        envelope["importacaoDoResultado"]!["relatorios"]![0]!["leiauteCodigo"] = codigo;
        LeitorEnvelope leitor = new();

        LeituraDaImportacaoDoResultado.Ler(leitor, envelope);

        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void Hash_FaseAlterada_MudaResumo()
    {
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        string original = perfil.HashHex(perfil.Serializar(Envelope(Declaracao(Fase))));

        perfil.HashHex(perfil.Serializar(Envelope(Declaracao(Guid.CreateVersion7())))).Should().NotBe(original);
    }
}
