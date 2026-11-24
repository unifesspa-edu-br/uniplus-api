namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.VersoesDoEnvelopeDeTeste;

/// <summary>Confere a integridade e a forma dos bytes congelados.</summary>
public sealed class RegistroCodecsEnvelopePerfilTests
{
    /// <summary>
    /// Bytes adulterados decodificam e recodificam IDENTICAMENTE — o round-trip passaria
    /// sem que eles correspondessem mais ao HashConfiguracao. É o hash, não o round-trip,
    /// que prova que os bytes são os que o ato congelou.
    /// </summary>
    [Fact(DisplayName = "Bytes adulterados não produzem o hash persistido — recusa antes de qualquer parse")]
    public void IntegridadeViolada_Recusa()
    {
        (VersaoConfiguracao versao, byte[] bytes) = VersaoRica();

        // A versão continua declarando o hash ORIGINAL — é o cenário de quem trocou os
        // bytes na coluna sem poder recomputar o hash da linha forense.
        VersaoConfiguracao comprometida = ComBytes(versao, ComPeriodoAdulterado(bytes));

        Result<EnvelopeReidratado> resultado = ExemploDeEnvelope.Registro.Reidratar(comprometida);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
    }

    /// <summary>
    /// Este é o caso de quem adultera E recomputa o hash. O hash bate — mas os bytes não
    /// estão na forma canônica (ADR-0100), e um envelope canônico é o que o encoder
    /// produz. Reserializar o que se leu tem de reproduzir os bytes; se não reproduz, o
    /// que está na coluna não é um envelope canônico, ainda que o hash seja coerente.
    /// </summary>
    [Fact(DisplayName = "Bytes não-canônicos (chaves fora de ordem) são recusados, mesmo com o hash batendo")]
    public void BytesNaoCanonicos_Recusa()
    {
        byte[] bytes = BytesDoExemplo();
        byte[] naoCanonicos = ComChavesDoTopoEmOrdemInversa(bytes);
        naoCanonicos.Should().NotEqual(bytes, "pré-condição: a reordenação tem de produzir bytes distintos");

        Result<EnvelopeReidratado> resultado = ReidratarComoAbertura(naoCanonicos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
    }

    /// <summary>
    /// Um envelope com <c>"presente"</c> duas vezes tem <b>duas leituras possíveis</b>, e o
    /// hash cobre as duas igualmente. Aceitar a última seria escolher em silêncio.
    /// </summary>
    [Fact(DisplayName = "Chave JSON duplicada é recusada — não vale 'a última ganha'")]
    public void ChaveDuplicada_Recusa()
    {
        string json = Encoding.UTF8.GetString(BytesDoExemplo());
        string duplicado = json.Replace("\"presente\":true", "\"presente\":true,\"presente\":false", StringComparison.Ordinal);
        duplicado.Should().NotBe(json, "pré-condição: a duplicação tem de ter sido aplicada");

        Result<EnvelopeReidratado> resultado = ReidratarComoAbertura(Encoding.UTF8.GetBytes(duplicado));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().BeOneOf(
            ErrosCodecEnvelope.EnvelopeMalformado,
            ErrosCodecEnvelope.IntegridadeViolada);
    }

    private static byte[] ComPeriodoAdulterado(byte[] bytes)
    {
        JsonObject adulterado = EnvelopeDosBytes(bytes);
        adulterado["periodo"]!["numero"] = "666/2026";

        return PerfilCanonicoV1.Instancia.Serializar(adulterado);
    }

    /// <summary>O encoder emite as chaves do topo em ordem ordinal; aqui elas saem na ordem inversa.</summary>
    private static byte[] ComChavesDoTopoEmOrdemInversa(byte[] bytes)
    {
        JsonObject foraDeOrdem = [];
        foreach (KeyValuePair<string, JsonNode?> par in EnvelopeDosBytes(bytes).Reverse().ToList())
        {
            foraDeOrdem[par.Key] = par.Value?.DeepClone();
        }

        return Encoding.UTF8.GetBytes(foraDeOrdem.ToJsonString());
    }
}
