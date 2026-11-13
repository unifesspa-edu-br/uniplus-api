namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// Os grupos coletados do envelope (UNI-REQ-0146): o grupo, os campos e os agregados fecham nas mesmas
/// invariantes da escrita, e as regras de derivação não citam o que existe por ocorrência.
/// </summary>
public sealed class EnvelopeCodecRecusaGruposColetadosTests
{
    // ── gruposColetados (UNI-REQ-0146): o grupo e os campos fecham nas mesmas invariantes da escrita ──

    [Theory(DisplayName = "gruposColetados com código ou campo repetido entre fatos e grupos, ou na ordem de um item, é recusado")]
    [InlineData("codigo", "RENDA")]
    [InlineData("campo", "RENDA")]
    [InlineData("ordem", "RENDA")]
    public void GruposColetados_ColisaoComItens_Recusa(string chave, string valor)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ColidirOGrupoComFatoOuOrdem(envelope, chave, valor));

        resultado.IsFailure.Should().BeTrue("o encoder nunca congela grupo que colide com fato ou com a ordem de um item");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain(
            "'gruposColetados'", "a colisão é recusada pela conferência do próprio bloco, que vale mesmo com o grafo congelado recomputado");
    }

    [Theory(DisplayName = "Agregado congelado que não agrega campo do grupo, com operação fora do vocabulário ou fora da ordem é recusado")]
    [InlineData("membro")]
    [InlineData("operacao")]
    [InlineData("ordem")]
    public void GruposColetados_AgregadoMalformado_Recusa(string defeito)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            MalformarOAgregado(AgregadosDoGrupo(envelope), defeito));

        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado, "o encoder só congela agregado sobre campo do grupo, com a operação do vocabulário e na ordem do código");
        resultado.Error.Message.Should().Contain("agregados[");
    }

    [Fact(DisplayName = "gruposColetados com dois grupos de mesmo código é recusado, mesmo com campos distintos")]
    public void GruposColetados_CodigoRepetido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(DuplicarOGrupoComOutrosCampos);

        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("'gruposColetados'");
    }

    [Fact(DisplayName = "regra de derivação que cita campo de grupo repetível é recusada — o campo existe por ocorrência")]
    public void RegrasDerivacao_CitandoCampoDeGrupo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject regimeDeIngresso = envelope["regrasDerivacao"]!.AsArray()
                .Single(r => r!["codigoFato"]!.GetValue<string>() == "REGIME_INGRESSO")!.AsObject();
            JsonObject condicao = regimeDeIngresso["regras"]!.AsArray()
                .Select(static r => r!["quando"])
                .OfType<JsonArray>()
                .First()[0]!.AsArray()[0]!.AsObject();
            condicao["fato"] = "TRABALHADOR_RURAL";
        });

        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("'regrasDerivacao'");
    }

    [Theory(DisplayName = "regra de derivação com o código de um grupo ou de um campo de grupo é recusada")]
    [InlineData("COMPOSICAO_FAMILIAR")]
    [InlineData("TRABALHADOR_RURAL")]
    public void RegrasDerivacao_ComCodigoDeGrupoOuDeCampo_Recusa(string codigo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["regrasDerivacao"]!.AsArray()
                .Single(r => r!["codigoFato"]!.GetValue<string>() == "REGIME_INGRESSO")!["codigoFato"] = codigo);

        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("'regrasDerivacao'");
    }

    [Fact(DisplayName = "gruposColetados com campo de seção própria é recusado — o campo segue o grupo")]
    public void GruposColetados_CampoComSecaoPropria_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["gruposColetados"]!.AsArray().Single()!["subitens"]!.AsArray()[0]!["etapaCodigo"] = "DADOS");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>Os agregados citam os campos pelo código: sem eles, a recusa é a da colisão.</summary>
    private static JsonObject GrupoSemAgregados(JsonObject envelope)
    {
        JsonObject grupo = envelope["gruposColetados"]!.AsArray().Single()!.AsObject();
        grupo["agregados"] = new JsonArray();

        return grupo;
    }

    private static void ColidirOGrupoComFatoOuOrdem(JsonObject envelope, string chave, string valor)
    {
        JsonObject grupo = GrupoSemAgregados(envelope);
        switch (chave)
        {
            case "codigo":
                grupo["codigo"] = valor;
                break;
            case "campo":
                grupo["subitens"]!.AsArray()[0]!["fatoCodigo"] = valor;
                break;
            default:
                grupo["ordem"] = OrdemDoFato(envelope, valor);
                break;
        }
    }

    private static int OrdemDoFato(JsonObject envelope, string codigo) =>
        FatoColetadoPorCodigo(envelope, codigo)["ordem"]!.GetValue<int>();

    private static JsonArray AgregadosDoGrupo(JsonObject envelope) =>
        envelope["gruposColetados"]!.AsArray().Single()!["agregados"]!.AsArray();

    private static void MalformarOAgregado(JsonArray agregados, string defeito)
    {
        switch (defeito)
        {
            case "membro":
                agregados[0]!["fatoDeMembro"] = "RENDA";
                break;
            case "operacao":
                agregados[0]!["operacao"] = "SOMA";
                break;
            default:
                MoverOPrimeiroAgregadoParaOFim(agregados);
                break;
        }
    }

    private static void MoverOPrimeiroAgregadoParaOFim(JsonArray agregados)
    {
        JsonNode primeiro = agregados[0]!.DeepClone();
        agregados.RemoveAt(0);
        agregados.Add(primeiro);
    }

    private static void DuplicarOGrupoComOutrosCampos(JsonObject envelope)
    {
        JsonArray grupos = envelope["gruposColetados"]!.AsArray();
        JsonObject copia = grupos[0]!.DeepClone().AsObject();
        copia["ordem"] = grupos[0]!["ordem"]!.GetValue<int>() + 1;
        copia["incluiCandidato"] = false;
        copia["agregados"] = new JsonArray();
        foreach (JsonNode? campo in copia["subitens"]!.AsArray())
        {
            campo!["fatoCodigo"] = $"{campo["fatoCodigo"]!.GetValue<string>()}_2";
        }

        grupos.Add(copia);
    }
}
