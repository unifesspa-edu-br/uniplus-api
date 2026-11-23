namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.VersoesDoEnvelopeDeTeste;

/// <summary>Confere as recusas de versão, integridade e coerência do envelope.</summary>
public sealed class EnvelopeCodecRecusaVersaoEIntegridadeTests
{
    // ── Versão desconhecida ──

    [Fact(DisplayName = "Versão fora do registro: recusa nomeada, não tentativa de leitura")]
    public void VersaoDesconhecida_Recusa()
    {
        VersaoConfiguracao versao = VersaoComSchema("9.9");

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoDesconhecida);
    }

    // ── Integridade forense: a evidência tem de provar o que diz provar ──

    [Fact(DisplayName = "Fase congelada sem a chave da unidade responsável é recusada: a forma anterior do envelope não é lida")]
    public void FaseSemAChaveDaUnidadeResponsavel_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["cronogramaFases"]!["fases"]![0]!.AsObject().Remove("unidadeResponsavel"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("unidadeResponsavel");
    }

    [Fact(DisplayName = "Etapas do formulário fora da ordem crescente são recusadas: o encoder nunca as emite assim")]
    public void EtapasForaDeOrdem_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray etapas = envelope["formularios"]![0]!["etapas"]!.AsArray();
            JsonNode primeira = etapas[0]!.DeepClone();
            etapas.RemoveAt(0);
            etapas.Add(primeira);
        });

        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Exigência que repete por grupo ausente dos formulários do envelope é recusada, como na escrita")]
    public void RepeticaoPorGrupoAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["regrasDeComprovacao"]![0]!["grupoDoFormularioParaRepeticao"] = "GRUPO_AUSENTE", comRegrasDeComprovacao: true);

        resultado.Error!.Code.Should().Be("RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado");
    }

    [Fact(DisplayName = "Escala da nota final que não é positiva é recusada na reidratação, como na gravação")]
    public void EscalaDaNotaFinalNaoPositiva_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["escalaNotaFinal"] = "0.0000");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EscalaNotaFinalInvalida");
    }

    [Fact(DisplayName = "Algoritmo de hash que o codec não emite é recusado")]
    public void AlgoritmoNaoSuportado_Recusa()
    {
        VersaoConfiguracao versao = VersaoComAlgoritmoDeHash("md5@v0");

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.AlgoritmoNaoSuportado);
    }

    // ── Coerência envelope × linha ──

    /// <summary>
    /// O envelope carrega o hash do corpus; a linha declara outro. Uma das duas evidências
    /// está errada — e não há como saber qual.
    /// </summary>
    [Fact(DisplayName = "O hash do documento no envelope tem de ser o do ato criador da versão")]
    public void HashDocumentoDivergente_Recusa()
    {
        VersaoConfiguracao versao = VersaoComHashDoAtoCriador(new string('7', 64));

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao);
    }

    [Fact(DisplayName = "A versão 1 não pode carregar o 18º bloco — ela abre a cadeia e não retifica ato algum")]
    public void Versao1ComBlocoDeRetificacao_Recusa()
    {
        byte[] bytes = BytesComRetificacaoDe(CorpusEnvelope.AtoAbertura, "motivo qualquer");

        Result<EnvelopeReidratado> resultado = ReidratarComoAbertura(bytes);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao);
    }

    [Fact(DisplayName = "A versão N>1 sem o 18º bloco é recusada — a cadeia de retificação não se perde")]
    public void VersaoSucessoraSemBlocoDeRetificacao_Recusa()
    {
        byte[] semRetificacao = BytesDoCorpus();

        Result<EnvelopeReidratado> resultado = ReidratarComoSucessora(semRetificacao, semRetificacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao);
    }

    [Fact(DisplayName = "O ato retificado no envelope tem de ser o que a versão registra ter emendado")]
    public void AtoRetificadoDivergente_Recusa()
    {
        byte[] retificada = BytesComRetificacaoDe(new Guid("01900000-0000-7000-8000-00000000dead"), "motivo");

        Result<EnvelopeReidratado> resultado = ReidratarComoSucessora(BytesDoCorpus(), retificada);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao);
    }

}
