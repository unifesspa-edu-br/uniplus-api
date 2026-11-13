namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.VersoesDoEnvelopeDeTeste;

/// <summary>
/// A gramática do envelope é fechada em todos os níveis: chave desconhecida, chave duplicada, bloco ausente
/// ou com a forma de um stub extinto e identificador legível fora da regra do cadastro são recusados — um
/// leitor tolerante perderia configuração em silêncio.
/// </summary>
public sealed class EnvelopeCodecRecusaGramaticaFechadaTests
{
    // ── A gramática é FECHADA em todos os níveis ──

    /// <summary>
    /// O caso crítico é o <c>bonusRegional</c>: um leitor que só olhasse <c>presente</c>
    /// leria <c>{"presente":false,"fator":"1.2000",…}</c> como “sem bônus” e
    /// <b>descartaria o bônus regional do certame</b> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) sem deixar rastro. É o modo
    /// de falha exato que esta story existe para tornar impossível.
    /// </summary>
    [Theory(DisplayName = "Chave desconhecida em QUALQUER nível é recusada — um leitor tolerante perde configuração em silêncio")]
    [InlineData("$")]
    [InlineData("bonusRegional")]
    [InlineData("periodo")]
    [InlineData("hashesEdital")]
    [InlineData("atendimento")]
    [InlineData("classificacao")]
    [InlineData("etapas.0")]
    [InlineData("distribuicao.0")]
    [InlineData("distribuicao.0.regraDistribuicao")]
    [InlineData("distribuicao.0.referenciaDemografica")]
    [InlineData("modalidades.0")]
    [InlineData("criteriosDesempate.0")]
    [InlineData("criteriosDesempate.0.args")]
    [InlineData("criteriosDesempate.0.regra")]
    [InlineData("criteriosDesempate.5.args")]
    [InlineData("classificacao.regrasEliminacao.0")]
    [InlineData("classificacao.regrasEliminacao.0.args")]
    [InlineData("classificacao.regrasEliminacao.0.regra")]
    [InlineData("classificacao.regraCalculo")]
    [InlineData("classificacao.quadroPesoAreaEnem.0")]
    [InlineData("classificacao.quadroPesoAreaEnem.0.grupoAreaEnem")]
    [InlineData("classificacao.quadroPesoAreaEnem.0.areas.0")]
    [InlineData("atendimento.condicoes.0")]
    [InlineData("atendimento.recursos.0")]
    [InlineData("atendimento.tiposDeficiencia.0")]
    [InlineData("bonusRegional.regra")]
    [InlineData("vagas.0")]
    [InlineData("cascataRemanejamento")]
    [InlineData("cascataRemanejamento.regra")]
    [InlineData("cascataRemanejamento.ordens.0")]
    [InlineData("identidadesUnidade")]
    [InlineData("identidadesUnidade.administradora")]
    [InlineData("divulgacao")]
    // Os dois objetos de regra OPCIONAIS e o bloco da convenção de contagem. Os obrigatórios
    // acima já cobriam o leitor compartilhado; estes três não passavam por caso nenhum, e um
    // fechamento de chaves esquecido neles não acusaria em lugar algum da suíte.
    [InlineData("classificacao.regraArredondamento")]
    [InlineData("distribuicao.0.regraAjuste")]
    [InlineData("algoritmoContagemPrazo")]
    public void ChaveDesconhecida_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject alvo = caminho == "$"
                ? envelope
                : Navegar(envelope, caminho);
            alvo["chaveIntrusa"] = "x";
        });

        resultado.IsFailure.Should().BeTrue(
            $"uma chave desconhecida em '{caminho}' significa que o envelope tem uma forma que este codec não " +
            "conhece — ignorá-la é a definição de perder dado em silêncio");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "O caso crítico: {presente:false} com args de bônus é RECUSADO, não lido como 'sem bônus'")]
    public void BonusPresenteFalsoComArgs_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(DeclararBonusAusenteMantendoOFator);

        resultado.IsFailure.Should().BeTrue(
            "um bônus 'ausente' que ainda carrega args é envelope contraditório. Lê-lo como 'sem bônus' descartaria " +
            "o bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) de um certame publicado, e ninguém veria.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Mesmo raciocínio de <see cref="BonusPresenteFalsoComArgs_Recusa"/>, agora para a
    /// cascata (Story #575): um leitor que só olhasse <c>presente</c> leria
    /// <c>{"presente":false,"regra":{...},...}</c> como "sem cascata" e descartaria a
    /// sequência legal de remanejamento (cascata de remanejamento de cotas, UNI-REQ-0056) de um certame publicado, sem
    /// deixar rastro — exatamente o modo de falha que <c>ExigirChaves</c> torna impossível.
    /// </summary>
    [Fact(DisplayName = "O caso crítico: {presente:false} com args de cascata é RECUSADO, não lido como 'sem cascata'")]
    public void CascataPresenteFalsoComArgs_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject cascata = envelope["cascataRemanejamento"]!.AsObject();
            cascata["presente"] = false;
            // Sobra `regra`, `fallbackCodigo` e `ordens` — a forma "ausente" só admite `presente`.
        });

        resultado.IsFailure.Should().BeTrue(
            "uma cascata 'ausente' que ainda carrega regra/fallback/ordens é envelope contraditório. Lê-la como " +
            "'sem cascata' descartaria a sequência legal de remanejamento (cascata de remanejamento de cotas, UNI-REQ-0056) de um certame publicado, " +
            "e ninguém veria.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Um envelope com <c>"presente"</c> duas vezes tem <b>duas leituras possíveis</b>, e o
    /// hash cobre as duas igualmente. Aceitar a última seria escolher em silêncio.
    /// </summary>
    /// <remarks>
    /// A recusa vem de <b>duas</b> guardas independentes, e é deliberado que venha: a forma
    /// canônica (um envelope com chave repetida não é o que <c>ComputeSnapshotBytes</c>
    /// produz) e o <c>AllowDuplicateProperties = false</c> no parse. A primeira é a que de
    /// fato dispara hoje — a segunda existe porque uma versão futura poderia ter forma
    /// canônica diferente, e a garantia não pode depender disso.
    /// </remarks>
    [Fact(DisplayName = "Chave JSON duplicada é recusada — não vale 'a última ganha'")]
    public void ChaveDuplicada_Recusa()
    {
        string json = Encoding.UTF8.GetString(BytesDoCorpus());
        string duplicado = json.Replace("\"presente\":true", "\"presente\":true,\"presente\":false", StringComparison.Ordinal);
        duplicado.Should().NotBe(json, "pré-condição: a duplicação tem de ter sido aplicada");

        Result<EnvelopeReidratado> resultado = ReidratarComoAbertura(Encoding.UTF8.GetBytes(duplicado));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().BeOneOf(
            ErrosCodecEnvelope.EnvelopeMalformado,
            ErrosCodecEnvelope.IntegridadeViolada);
    }

    [Fact(DisplayName = "Chave desconhecida DENTRO do 18º bloco (retificacao) é recusada")]
    public void ChaveDesconhecidaNaRetificacao_Recusa()
    {
        JsonObject envelope = EnvelopeDosBytes(BytesComRetificacaoDe(CorpusEnvelope.AtoAbertura, "motivo"));
        envelope["retificacao"]!.AsObject()["chaveIntrusa"] = "x";

        byte[] retificada = PerfilCanonicoV1.Instancia.Serializar(envelope);
        Result<EnvelopeReidratado> resultado = ReidratarComoSucessora(BytesDoCorpus(), retificada);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Bloco real com chaves de um formato de stub extinto é forma intrusa — recusado, não tolerado")]
    [InlineData("vagas")]
    [InlineData("documentosExigidos")]
    [InlineData("cascataRemanejamento")]
    public void BlocoRealComFormaDeStubExtinto_Recusa(string bloco)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope[bloco] = new JsonObject { ["status"] = "construido", ["itens"] = new JsonArray() });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Bloco obrigatório ausente é recusado")]
    public void BlocoAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope.Remove("atendimento"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    // ── Identificador legível congelado: o decoder aplica a mesma regra do cadastro. Um valor que
    // o value object recusaria não sai do encoder, então achá-lo é sinal de bytes que não vieram
    // desse caminho. ──

    [Fact(DisplayName = "Identificador legível ausente do envelope é recusado")]
    public void IdentificadorLegivelAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope.Remove("identificadorLegivel"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("identificadorLegivel");
    }

    [Fact(DisplayName = "Identificador legível que não é texto é recusado")]
    public void IdentificadorLegivelNaoTexto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope["identificadorLegivel"] = 2026);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("identificadorLegivel");
    }

    [Theory(DisplayName = "Identificador legível fora da regra do cadastro é recusado com a causa do cadastro")]
    [InlineData("PSIQ 2026", ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido)]
    [InlineData("ps", ProcessoSeletivoErrorCodes.IdentificadorLegivelTamanho)]
    [InlineData("a1b2c3d4-e5f6-4789-8abc-def012345678", ProcessoSeletivoErrorCodes.IdentificadorLegivelComFormatoDeGuid)]
    public void IdentificadorLegivelInvalido_Recusa(string valor, string codigoEsperado)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope["identificadorLegivel"] = valor);

        resultado.IsFailure.Should().BeTrue(
            "o endereço público congelado tem de obedecer à mesma regra que o cadastro impõe");
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    private static readonly string[] ArgsDoBonusAlemDoFator =
    [
        "regra", "teto", "baseLegalBonusRegionalId", "tipoInstrumento", "identificacao", "descricao", "municipios",
    ];

    /// <summary>Sobra `fator` — a forma "ausente" só admite `presente`.</summary>
    private static void DeclararBonusAusenteMantendoOFator(JsonObject envelope)
    {
        JsonObject bonus = envelope["bonusRegional"]!.AsObject();
        bonus["presente"] = false;
        foreach (string chave in ArgsDoBonusAlemDoFator)
        {
            bonus.Remove(chave);
        }
    }
}
