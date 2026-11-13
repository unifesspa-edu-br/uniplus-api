namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.VersoesDoEnvelopeDeTeste;

/// <summary>
/// <b>O que a reidratação recusa</b> — e por quê: versão desconhecida do envelope, versão
/// conhecida mas não reidratável (ex.: a 1.0, que podia congelar classificação como stub),
/// e blocos derivados que não fecham entre si (Story #859, critérios de aceite sobre versão
/// e integridade dos blocos derivados; ADR-0110 sobre codec nunca aposentado e sobre o
/// bloco 'nao_construido' banido).
/// </summary>
/// <remarks>
/// Cada teste aqui é uma contraprova: sem a guarda que ele exercita, o envelope
/// reidrataria <b>com sucesso</b> num agregado que nunca existiu. É por isso que a recusa
/// é <b>nomeada</b> — um descarte que falha precisa dizer ao operador se o problema é uma
/// versão que o sistema não conhece, uma que ele conhece e não reidrata, ou uma evidência
/// que não prova o que diz provar.
/// </remarks>
public sealed class EnvelopeCodecRecusaVersaoEIntegridadeTests
{
    // ── Versão desconhecida e versão conhecida-não-reidratável ──

    [Fact(DisplayName = "Versão fora do registro: recusa nomeada, não tentativa de leitura")]
    public void VersaoDesconhecida_Recusa()
    {
        VersaoConfiguracao versao = VersaoComSchema("9.9");

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(versao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoDesconhecida);
    }

    // ── Integridade forense: a evidência tem de provar o que diz provar ──

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

        Result<EnvelopeReidratado> resultado = CorpusEnvelope.Registro.Reidratar(comprometida);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
    }

    /// <summary>
    /// Este é o caso de quem adultera E recomputa o hash. O hash bate — mas os bytes não
    /// estão na forma canônica (ADR-0100), e um envelope canônico é o que o encoder
    /// produz. Reserializar o que se leu tem de reproduzir os bytes; se não reproduz, o
    /// que está na coluna não é um envelope 1.1, é outra coisa com um hash coerente.
    /// </summary>
    [Fact(DisplayName = "Bytes não-canônicos (chaves fora de ordem) são recusados, mesmo com o hash batendo")]
    public void BytesNaoCanonicos_Recusa()
    {
        byte[] bytes = BytesDoCorpus();
        byte[] naoCanonicos = ComChavesDoTopoEmOrdemInversa(bytes);
        naoCanonicos.Should().NotEqual(bytes, "pré-condição: a reordenação tem de produzir bytes distintos");

        Result<EnvelopeReidratado> resultado = ReidratarComoAbertura(naoCanonicos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
    }

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
            envelope["arvoreSatisfacao"]![0]!["repetePorEntidade"] = "GRUPO_AUSENTE", comArvoreSatisfacao: true);

        resultado.Error!.Code.Should().Be("NoExigencia.TipoEntidadeInvalido");
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
