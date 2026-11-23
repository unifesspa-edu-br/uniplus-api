namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Cenarios;

public sealed class ChaveDaEtapaNoEnvelopeTests
{
    private static JsonObject EtapaGravada()
    {
        JsonObject documento = new() { ["etapas"] = SerializacaoDasEtapas.SerializarEtapas(ProcessoComEtapaBuilder.Criar()) };
        byte[] bytes = PerfilCanonicoV1.Instancia.Serializar(documento);
        return JsonNode.Parse(bytes)!["etapas"]![0]!.DeepClone().AsObject();
    }

    private static IReadOnlyList<EtapaProcesso> Ler(LeitorEnvelope leitor, params JsonObject[] etapas) =>
        LeituraDasEtapas.LerEtapas(leitor, new JsonObject { ["etapas"] = new JsonArray(etapas) });

    [Fact]
    public void EscritaELeitura_EtapaComChave_PreservamNaReidratacao()
    {
        JsonObject gravada = EtapaGravada();
        LeitorEnvelope leitor = new();
        IReadOnlyList<EtapaProcesso> lidas = Ler(leitor, gravada);

        leitor.Falhou.Should().BeFalse();
        gravada["chave"]!.GetValue<string>().Should().Be("etapa_prova_2");
        lidas.Single().Chave.Valor.Should().Be("etapa_prova_2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Etapa")]
    [InlineData("etapa__prova")]
    public void Leitura_ChaveAusenteOuMalformada_Recusa(string? chave)
    {
        JsonObject gravada = EtapaGravada();
        if (chave is null)
        {
            gravada.Remove("chave");
        }
        else
        {
            gravada["chave"] = chave;
        }

        LeitorEnvelope leitor = new();
        Ler(leitor, gravada);
        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("chave");
    }

    [Fact]
    public void Leitura_ChaveRepetida_Recusa()
    {
        JsonObject gravada = EtapaGravada();
        LeitorEnvelope leitor = new();
        Ler(leitor, gravada, (JsonObject)gravada.DeepClone());

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("etapa_prova_2");
    }
}
