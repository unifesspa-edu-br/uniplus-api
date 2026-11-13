namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// As etapas do envelope: itens de avaliação, escala de atendimento, o snapshot congelado do tipo de etapa
/// (issue #1071) e o teto de cada código que vai para uma coluna limitada — o decoder recusa antes de o
/// INSERT do descarte estourar.
/// </summary>
public sealed class EnvelopeCodecRecusaEtapasTests
{
    // ── itens de avaliação e escala de atendimento da etapa ──

    [Theory(DisplayName = "Item ou nível adulterado no envelope é recusado: pontuação não positiva, fração fora de 0 a 1, escala vazia, item sem chave")]
    [InlineData("pontuacao")]
    [InlineData("fracao")]
    [InlineData("escalaVazia")]
    [InlineData("itemSemRotulo")]
    public void ItensEEscala_Adulterados_Recusa(string adulteracao)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            AdulterarItemOuEscala(envelope, adulteracao));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    // ── issue #1071 — etapas[].tipoEtapa: snapshot congelado do tipo de etapa ──

    [Fact(DisplayName = "etapas[].tipoEtapa ausente é recusado")]
    public void TipoEtapa_Ausente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!.AsObject().Remove("tipoEtapa"));

        resultado.IsFailure.Should().BeTrue(
            "sem tipoEtapa não há como reidratar a identidade estável que AvaliarEtapaObrigatoria compara");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "etapas[].tipoEtapa com chave obrigatória ausente é recusado")]
    [InlineData("origemId")]
    [InlineData("codigo")]
    [InlineData("nome")]
    [InlineData("admitePontuacao")]
    [InlineData("admiteEliminacao")]
    [InlineData("notaDeOrigemNoEnem")]
    public void TipoEtapa_ChaveAusente_Recusa(string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!.AsObject().Remove(chave));

        resultado.IsFailure.Should().BeTrue(
            $"'{chave}' fecha a gramática de tipoEtapa — o encoder sempre a emite");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].tipoEtapa com chave intrusa é recusado")]
    public void TipoEtapa_ChaveIntrusa_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!["descricao"] = "campo que o encoder nunca emitiu");

        resultado.IsFailure.Should().BeTrue("a gramática de tipoEtapa é fechada — igual a qualquer outro bloco do envelope");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "etapas[].tipoEtapa com sinalizador que não é booleano é recusado")]
    [InlineData("admitePontuacao")]
    [InlineData("admiteEliminacao")]
    [InlineData("notaDeOrigemNoEnem")]
    public void TipoEtapa_SinalizadorNaoBooleano_Recusa(string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]![chave] = "true");

        resultado.IsFailure.Should().BeTrue(
            $"'{chave}' é booleano no encoder; o texto \"true\" não é o mesmo documento");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].tipoEtapa que não pontua nem elimina é recusado")]
    public void TipoEtapa_SemCaraterAdmitido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonNode tipoEtapa = envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!;
            tipoEtapa["admitePontuacao"] = false;
            tipoEtapa["admiteEliminacao"] = false;
        });

        resultado.IsFailure.Should().BeTrue(
            "um tipo sem caráter admitido não existe no cadastro — a factory do snapshot o recusa");
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.SemCaraterAdmitido");
    }

    [Fact(DisplayName = "etapas[].tipoEtapa com nota de origem no ENEM que não pontua é recusado")]
    public void TipoEtapa_NotaDeOrigemNoEnemSemPontuacao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonNode tipoEtapa = envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!;
            tipoEtapa["notaDeOrigemNoEnem"] = true;
            tipoEtapa["admitePontuacao"] = false;
            tipoEtapa["admiteEliminacao"] = true;
        });

        resultado.IsFailure.Should().BeTrue(
            "o cadastro não admite tipo com nota do ENEM que não pontue — a factory do snapshot o recusa");
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.NotaDeOrigemNoEnemSemPontuacao");
    }

    [Fact(DisplayName = "etapas[].tipoEtapa.origemId vazio (Guid.Empty) é recusado")]
    public void TipoEtapa_OrigemIdVazio_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!["origemId"] = Guid.Empty.ToString());

        resultado.IsFailure.Should().BeTrue(
            "um snapshot sem origem não prova de qual tipo do cadastro a etapa foi copiada");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "etapas[].tipoEtapa.codigo ou nome vazio é recusado")]
    [InlineData("codigo")]
    [InlineData("nome")]
    public void TipoEtapa_CodigoOuNomeVazio_Recusa(string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]![chave] = "");

        resultado.IsFailure.Should().BeTrue(
            $"'{chave}' vazio produziria um snapshot que a própria factory do domínio (TipoEtapaSnapshot.Criar) recusa");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].tipoEtapa.codigo acima do limite da coluna (64) é recusado")]
    public void TipoEtapa_CodigoAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!["codigo"] = new string('A', 65));

        resultado.IsFailure.Should().BeTrue(
            "um código de 65 caracteres não cabe na coluna tipo_etapa_codigo (varchar 64) — recusar aqui evita " +
            "DbUpdateException (500) no meio do descarte");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].tipoEtapa.nome acima do limite da coluna (200) é recusado")]
    public void TipoEtapa_NomeAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["tipoEtapa"]!["nome"] = new string('A', 201));

        resultado.IsFailure.Should().BeTrue(
            "um nome de 201 caracteres não cabe na coluna tipo_etapa_nome (varchar 200)");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "etapas[].faseCodigo acima do limite da coluna (60) é recusado")]
    public void Etapa_FaseCodigoAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["faseCodigo"] = new string('A', 61));

        resultado.IsFailure.Should().BeTrue(
            "um código de 61 caracteres não cabe na coluna fase_codigo (varchar 60) — o teto do decodificador " +
            "media 300, então o valor passava aqui para estourar no INSERT, com 500 no meio do descarte");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);

        // EnvelopeMalformado é o código de dezenas de guardas. Sem prender a mensagem ao campo
        // e ao teto, uma guarda futura que recusasse este envelope por outro motivo — o código
        // de fase que não resolve no cronograma, por exemplo — deixaria o teste verde com o
        // limite de volta em 300, que é exatamente a regressão que ele existe para prender.
        resultado.Error.Message.Should().Contain("etapas[0].faseCodigo").And.Contain("60");
    }

    [Fact(DisplayName = "etapas[].produtos[].atoCodigo acima do limite da coluna (60) é recusado")]
    public void Etapa_ProdutoAtoCodigoAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["produtos"]!.AsArray()[0]!["atoCodigo"] = new string('A', 61));

        resultado.IsFailure.Should().BeTrue(
            "um código de 61 caracteres não cabe na coluna ato_codigo de produtos_da_etapa (varchar 60)");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("etapas[0].produtos[0].atoCodigo").And.Contain("60");
    }

    [Fact(DisplayName = "etapas[].bancas[].codigo acima do limite da coluna (60) é recusado")]
    public void Etapa_BancaCodigoAcimaDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["etapas"]!.AsArray()[0]!["bancas"]!.AsArray()[0]!["codigo"] = new string('A', 61));

        resultado.IsFailure.Should().BeTrue(
            "um código de 61 caracteres não cabe na coluna codigo de bancas_da_etapa (varchar 60)");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("etapas[0].bancas[0].codigo").And.Contain("60");
    }

    private static void AdulterarItemOuEscala(JsonObject envelope, string adulteracao)
    {
        JsonObject objetiva = envelope["etapas"]!.AsArray()[0]!.AsObject();
        JsonObject defesa = envelope["etapas"]!.AsArray()[3]!.AsObject();
        Action adulterar = adulteracao switch
        {
            "pontuacao" => () => objetiva["itensDeAvaliacao"]![0]!["pontuacaoMaxima"] = "0.0000",
            "fracao" => () => defesa["escalaDeAtendimento"]![0]!["fracao"] = "1.5000",
            "escalaVazia" => () => defesa["escalaDeAtendimento"] = new JsonArray(),
            _ => () => objetiva["itensDeAvaliacao"]![0]!.AsObject().Remove("rotulo"),
        };

        adulterar();
    }
}
