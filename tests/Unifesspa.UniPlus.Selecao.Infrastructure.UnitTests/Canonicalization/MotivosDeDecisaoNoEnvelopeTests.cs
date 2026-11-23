namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class MotivosDeDecisaoNoEnvelopeTests
{
    private static MotivoDeDecisaoDeclarado Motivo(string codigo, LugarDaDecisao lugar) => new()
    {
        Tipo = TipoMotivoDeDecisao.AnaliseDocumental,
        Codigo = codigo,
        Lugar = lugar,
        Descricao = "Documentação conferida",
        Sentido = SentidoDoMotivo.Favoravel,
        VersaoDeEntrada = 1,
    };

    private static MotivoDeDecisaoDeclarado[] Escolhas() =>
    [
        Motivo("B", LugarDaDecisao.DaEtapa(Guid.Parse("22222222-2222-2222-2222-222222222222"))),
        Motivo("A", LugarDaDecisao.DaFase(Guid.Parse("11111111-1111-1111-1111-111111111111"))),
    ];

    [Fact]
    public void Serializar_EntradaEmbaralhada_OrdenaEEscreveCampos()
    {
        JsonArray bloco = SerializacaoDosMotivosDeDecisao.Serializar(Escolhas());
        JsonObject primeiro = bloco[0]!.AsObject();

        primeiro["codigo"]!.GetValue<string>().Should().Be("A");
        primeiro["tipoDeDecisaoCodigo"]!.GetValue<string>().Should().Be("ANALISE_DOCUMENTAL");
        primeiro["lugar"]!["tipo"]!.GetValue<string>().Should().Be("FASE");
        primeiro["descricao"]!.GetValue<string>().Should().Be("Documentação conferida");
        primeiro["sentido"]!.GetValue<string>().Should().Be("FAVORAVEL");
        primeiro["versaoDoEditalDeEntrada"]!.GetValue<int>().Should().Be(1);
        primeiro.ContainsKey("subclassificacao").Should().BeTrue();
        primeiro["subclassificacao"].Should().BeNull();
        primeiro.ContainsKey("justificativaDaInclusao").Should().BeTrue();
        primeiro["justificativaDaInclusao"].Should().BeNull();
    }

    [Fact]
    public void Serializar_SemMotivos_EscreveListaVazia() =>
        SerializacaoDosMotivosDeDecisao.Serializar([]).Should().BeEmpty();

    [Fact]
    public void Serializar_OrdemDiferente_ProduzMesmoBloco()
    {
        MotivoDeDecisaoDeclarado[] escolhas = Escolhas();
        JsonArray original = SerializacaoDosMotivosDeDecisao.Serializar(escolhas);
        JsonArray invertida = SerializacaoDosMotivosDeDecisao.Serializar(escolhas.Reverse());

        JsonNode.DeepEquals(original, invertida).Should().BeTrue();
    }

    [Fact]
    public void Hash_MotivoAlterado_MudaResumo()
    {
        JsonObject envelope = new() { ["motivosDeDecisao"] = SerializacaoDosMotivosDeDecisao.Serializar(Escolhas()) };
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        string original = perfil.HashHex(perfil.Serializar(envelope));

        envelope["motivosDeDecisao"]![0]!["descricao"] = "Outro motivo";

        perfil.HashHex(perfil.Serializar(envelope)).Should().NotBe(original);
    }

    [Fact]
    public void Ler_BlocoSerializado_PreservaOsBytes()
    {
        JsonObject envelope = new() { ["motivosDeDecisao"] = SerializacaoDosMotivosDeDecisao.Serializar(Escolhas()) };
        LeitorEnvelope leitor = new();
        IReadOnlyList<MotivoDeDecisaoDeclarado> lidos = LeituraDosMotivosDeDecisao.Ler(leitor, envelope);

        leitor.Falhou.Should().BeFalse();
        JsonNode.DeepEquals(envelope["motivosDeDecisao"], SerializacaoDosMotivosDeDecisao.Serializar(lidos)).Should().BeTrue();
    }

    [Fact]
    public void Ler_BlocoAusente_Recusa()
    {
        LeitorEnvelope leitor = new();
        LeituraDosMotivosDeDecisao.Ler(leitor, []);
        leitor.Falhou.Should().BeTrue();
    }

    [Theory]
    [InlineData("tipoDeDecisaoCodigo")]
    [InlineData("codigo")]
    [InlineData("lugar")]
    [InlineData("descricao")]
    [InlineData("subclassificacao")]
    [InlineData("sentido")]
    [InlineData("versaoDoEditalDeEntrada")]
    [InlineData("justificativaDaInclusao")]
    public void Ler_ChaveDoMotivoAusente_Recusa(string chave)
    {
        JsonArray bloco = SerializacaoDosMotivosDeDecisao.Serializar(Escolhas());
        bloco[0]!.AsObject().Remove(chave);
        LeitorEnvelope leitor = new();
        LeituraDosMotivosDeDecisao.Ler(leitor, new JsonObject { ["motivosDeDecisao"] = bloco });
        leitor.Falhou.Should().BeTrue();
    }
}
