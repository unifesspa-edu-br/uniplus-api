namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.VersoesDoEnvelopeDeTeste;

/// <summary>
/// O bloco <c>divulgacao</c>, promovido de stub a real, precisa de um decoder tão estrito quanto o encoder
/// (issue #563). Round-trip verde prova que o que foi emitido sobrevive; não prova que o decoder recusa o
/// que não deveria aceitar.
/// </summary>
public sealed class EnvelopeCodecRecusaDivulgacaoTests
{
    /// <summary>
    /// Cada linha de <see cref="MutacoesDeDivulgacaoDoEnvelope"/> adultera um aspecto do bloco e assere o
    /// código E o caminho ofensivos (via trecho da mensagem).
    /// </summary>
    [Theory(DisplayName = "issue #563 (CA-12): cada mutação do bloco divulgacao ataca UMA invariante e é recusada com o código e o caminho esperados")]
    [MemberData(nameof(MutacoesDeDivulgacaoDoEnvelope.Todas), MemberType = typeof(MutacoesDeDivulgacaoDoEnvelope))]
    public void Divulgacao_CadaMutacaoAtacaUmaInvariante_Recusa(
        string descricao, Action<JsonObject> mutar, string codigoEsperado, string trechoEsperado)
    {
        ArgumentNullException.ThrowIfNull(mutar);

        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(mutar);

        resultado.IsFailure.Should().BeTrue($"a mutação '{descricao}' tem de ser recusada");
        resultado.Error!.Code.Should().Be(codigoEsperado, $"a mutação '{descricao}' tem de recusar com o código específico da invariante que ela ataca, não com 'IsFailure' por qualquer outro motivo");
        resultado.Error!.Message.Should().Contain(trechoEsperado, $"a mutação '{descricao}' tem de apontar para o campo ofensivo, não só recusar pelo código genérico");
    }

    /// <summary>
    /// O par do primeiro caso da teoria de recusa acima ("justificativa já em NFC com espaço
    /// nas bordas"): aqui a adulteração é NFD de verdade — "c" + combinante cedilha (U+0327) e
    /// "a" + combinante til (U+0303), sem espaço nas bordas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>O código esperado é <see cref="ErrosCodecEnvelope.IntegridadeViolada"/>, não
    /// <see cref="ErrosCodecEnvelope.EnvelopeMalformado"/></b> — e essa é a prova em si, não um
    /// acidente de teste. Decomposição Unicode genuína em QUALQUER string do envelope já é
    /// recusada por <c>RegistroCodecsEnvelope.VerificarFormaCanonica</c>, o gate universal que
    /// roda ANTES do decoder específico da versão: reserializar o payload inteiro pelo perfil
    /// canônico aplica NFC a toda string de negócio, produz bytes diferentes dos que chegaram, e
    /// o gate recusa por integridade — o decoder nem chega a rodar. O guard de forma canônica
    /// dentro de <c>LerDivulgacao</c> só é alcançável pelo caso de espaço nas bordas (acima): NFC
    /// não mexe em espaço, então esse caso passa incólume pelo gate universal e só é pego pelo
    /// guard específico do bloco <c>divulgacao</c>.
    /// </para>
    /// <para>
    /// Não usa <see cref="ReidratarComEnvelopeAdulterado"/>: aquele helper produz os bytes finais
    /// com <see cref="PerfilCanonicoV1.Serializar"/>, que "curaria" a decomposição antes mesmo de
    /// os bytes existirem — o teste nunca conseguiria observar o gate rejeitando nada. Aqui os
    /// bytes são escritos por um serializador comum, preservando os combinantes tal como foram
    /// escritos.
    /// </para>
    /// </remarks>
    [Fact(DisplayName = "issue #563 (CA-12): justificativa em NFD real é recusada pelo gate universal de forma canônica, antes de o decoder da versão rodar")]
    public void Divulgacao_JustificativaEmNfdReal_Recusa()
    {
        byte[] originais = BytesDoCorpus();
        JsonObject envelope = EnvelopeDosBytes(originais);
        envelope["divulgacao"]!.AsObject()["justificativa"] = $"Divulgac{(char)0x0327}a{(char)0x0303}o real";

        byte[] adulterados = Encoding.UTF8.GetBytes(envelope.ToJsonString());
        adulterados.Should().NotEqual(originais, "pré-condição: a adulteração tem de mudar os bytes");

        Result<EnvelopeReidratado> resultado = ReidratarComoAbertura(adulterados);

        resultado.IsFailure.Should().BeTrue("a justificativa decomposta (NFD) não está na forma canônica que o gate universal exige");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
        resultado.Error!.Message.Should().Contain("forma canônica");
    }
}
