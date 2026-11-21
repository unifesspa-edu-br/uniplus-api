namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

/// <summary>
/// Os tipos de decisão que a fase e a etapa declaram são escritos no envelope em ordem alfabética, sempre
/// presentes, e a leitura só aceita a forma que a escrita produz.
/// </summary>
public sealed class EnvelopeCodecTiposDeDecisaoTests
{
    private const string Classificacao = "CLASSIFICACAO";
    private const string IsencaoDaTaxa = "ISENCAO_DA_TAXA";
    private const string AnaliseDocumental = "ANALISE_DOCUMENTAL";

    private const string CaminhoDaFase = "cronogramaFases.fases.0";
    private const string CaminhoDaEtapa = "etapas.0";

    [Fact(DisplayName = "A fase escreve os tipos de decisão em ordem alfabética")]
    public void Fase_EscreveOsTiposEmOrdemAlfabetica()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico(
            tiposDeDecisaoDaFaseFinal: [IsencaoDaTaxa, Classificacao, AnaliseDocumental]);

        JsonObject envelope = Envelope(Codificar(processo));

        TiposDaFaseFinal(envelope).Should().Equal(AnaliseDocumental, Classificacao, IsencaoDaTaxa);
    }

    [Fact(DisplayName = "A etapa escreve os tipos de decisão em ordem alfabética")]
    public void Etapa_EscreveOsTiposEmOrdemAlfabetica()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        ProvaObjetivaDe(processo).DefinirTiposDeDecisao([IsencaoDaTaxa, Classificacao, AnaliseDocumental]).IsSuccess.Should().BeTrue();

        JsonObject envelope = Envelope(Codificar(processo));
        Guid id = ProvaObjetivaDe(processo).Id;

        Textos(envelope["etapas"]!.AsArray().Single(etapa => etapa!["id"]!.GetValue<Guid>() == id)!["tiposDeDecisao"]!)
            .Should().Equal(AnaliseDocumental, Classificacao, IsencaoDaTaxa);
    }

    [Fact(DisplayName = "Sem tipos declarados, a fase e a etapa escrevem a chave com a lista vazia")]
    public void SemTipos_EscreveAChaveVazia()
    {
        JsonObject envelope = Envelope(Codificar(CorpusEnvelopeProcessoRico.ProcessoRico()));

        JsonNode fase = envelope["cronogramaFases"]!["fases"]!.AsArray().Single(static fase => fase!["codigo"]!.GetValue<string>() == "INSCRICAO")!;
        JsonNode etapa = envelope["etapas"]!.AsArray().Single(static etapa => etapa!["nome"]!.GetValue<string>() == "Prova Objetiva")!;

        fase.AsObject().ContainsKey("tiposDeDecisao").Should().BeTrue();
        Textos(fase["tiposDeDecisao"]!).Should().BeEmpty();
        etapa.AsObject().ContainsKey("tiposDeDecisao").Should().BeTrue();
        Textos(etapa["tiposDeDecisao"]!).Should().BeEmpty();
    }

    [Fact(DisplayName = "Declarar os mesmos tipos de decisão da fase em outra ordem produz os mesmos bytes e o mesmo hash")]
    public void Fase_MesmosTiposEmOutraOrdem_MesmosBytesEHash()
    {
        SnapshotCanonico alfabetica = Codificar(CorpusEnvelopeProcessoRico.ProcessoRico(tiposDeDecisaoDaFaseFinal: [AnaliseDocumental, Classificacao]));
        SnapshotCanonico invertida = Codificar(CorpusEnvelopeProcessoRico.ProcessoRico(tiposDeDecisaoDaFaseFinal: [Classificacao, AnaliseDocumental]));

        invertida.Bytes.Should().Equal(alfabetica.Bytes);
        HashCanonicalComputer.ComputeSha256Hex(invertida.Bytes).Should().Be(HashCanonicalComputer.ComputeSha256Hex(alfabetica.Bytes));
    }

    [Fact(DisplayName = "Declarar os mesmos tipos de decisão da etapa em outra ordem produz os mesmos bytes")]
    public void Etapa_MesmosTiposEmOutraOrdem_MesmosBytes()
    {
        ProcessoSeletivo alfabetica = CorpusEnvelopeProcessoRico.ProcessoRico();
        ProcessoSeletivo invertida = CorpusEnvelopeProcessoRico.ProcessoRico();
        ProvaObjetivaDe(alfabetica).DefinirTiposDeDecisao([AnaliseDocumental, Classificacao]).IsSuccess.Should().BeTrue();
        ProvaObjetivaDe(invertida).DefinirTiposDeDecisao([Classificacao, AnaliseDocumental]).IsSuccess.Should().BeTrue();

        Codificar(invertida).Bytes.Should().Equal(Codificar(alfabetica).Bytes);
    }

    [Fact(DisplayName = "Ida e volta do envelope com tipos de decisão reproduz os bytes e o hash congelados")]
    public void IdaEVolta_ComTipos_ReproduzBytesEHash()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico(tiposDeDecisaoDaFaseFinal: [Classificacao, IsencaoDaTaxa]);
        ProvaObjetivaDe(processo).DefinirTiposDeDecisao([Classificacao]).IsSuccess.Should().BeTrue();
        SnapshotCanonico congelado = Codificar(processo);
        CorpusEnvelopeEntrada.Publicar(processo);
        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);

        AssertRoundTrip(processo, versao, congelado);

        HashCanonicalComputer.ComputeSha256Hex(congelado.Bytes).Should().Be(versao.HashConfiguracao);
    }

    [Fact(DisplayName = "O decodificador lê os tipos de decisão da fase: mudá-los no envelope muda o que a leitura devolve")]
    public void Decodificador_LeOsTiposDaFase()
    {
        EnvelopeReidratado envelope = ReidratarMutado(mutado => DefinirTipos(mutado, CaminhoDaFase, AnaliseDocumental, Classificacao));

        envelope.Configuracao.Cronograma.CronogramaFases[0].TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.Classificacao);
    }

    [Fact(DisplayName = "O decodificador lê os tipos de decisão da etapa: mudá-los no envelope muda o que a leitura devolve")]
    public void Decodificador_LeOsTiposDaEtapa()
    {
        EnvelopeReidratado envelope = ReidratarMutado(mutado => DefinirTipos(mutado, CaminhoDaEtapa, AnaliseDocumental, Classificacao));

        envelope.Configuracao.Avaliacao.Etapas[0].TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.Classificacao);
    }

    [Theory(DisplayName = "A leitura recusa tipos de decisão da fase fora da forma que a escrita produz")]
    [InlineData("fora de ordem", "CLASSIFICACAO,ANALISE_DOCUMENTAL")]
    [InlineData("repetido", "ANALISE_DOCUMENTAL,ANALISE_DOCUMENTAL")]
    [InlineData("desconhecido", "ANALISE_DOCUMENTAL,TIPO_QUE_NAO_EXISTE")]
    [InlineData("em minúsculas", "analise_documental")]
    public void Fase_TiposForaDaForma_Recusa(string defeito, string tipos)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            DefinirTipos(envelope, CaminhoDaFase, [.. tipos.Split(',')]));

        ConferirRecusa(resultado, defeito);
    }

    [Theory(DisplayName = "A leitura recusa tipos de decisão da etapa fora da forma que a escrita produz")]
    [InlineData("fora de ordem", "CLASSIFICACAO,ANALISE_DOCUMENTAL")]
    [InlineData("repetido", "ANALISE_DOCUMENTAL,ANALISE_DOCUMENTAL")]
    [InlineData("desconhecido", "ANALISE_DOCUMENTAL,TIPO_QUE_NAO_EXISTE")]
    [InlineData("em minúsculas", "analise_documental")]
    public void Etapa_TiposForaDaForma_Recusa(string defeito, string tipos)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            DefinirTipos(envelope, CaminhoDaEtapa, [.. tipos.Split(',')]));

        ConferirRecusa(resultado, defeito);
    }

    [Theory(DisplayName = "A leitura recusa a fase sem a chave dos tipos de decisão, ou com ela nula")]
    [InlineData(false)]
    [InlineData(true)]
    public void Fase_ChaveAusenteOuNula_Recusa(bool nula)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            AusentarOuAnular(NoDe(envelope, CaminhoDaFase), nula));

        ConferirRecusa(resultado, nula ? "nula" : "ausente");
    }

    [Theory(DisplayName = "A leitura recusa a etapa sem a chave dos tipos de decisão, ou com ela nula")]
    [InlineData(false)]
    [InlineData(true)]
    public void Etapa_ChaveAusenteOuNula_Recusa(bool nula)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            AusentarOuAnular(NoDe(envelope, CaminhoDaEtapa), nula));

        ConferirRecusa(resultado, nula ? "nula" : "ausente");
    }

    private static void ConferirRecusa(Result<EnvelopeReidratado> resultado, string defeito)
    {
        resultado.IsFailure.Should().BeTrue($"o defeito '{defeito}' não é a forma que a escrita produz");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("tiposDeDecisao");
    }

    private static SnapshotCanonico Codificar(ProcessoSeletivo processo) =>
        CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));

    private static EtapaProcesso ProvaObjetivaDe(ProcessoSeletivo processo) =>
        processo.Etapas.Single(static etapa => etapa.Nome == "Prova Objetiva");

    private static string[] TiposDaFaseFinal(JsonObject envelope) =>
        Textos(envelope["cronogramaFases"]!["fases"]!.AsArray()
            .Single(static fase => fase!["codigo"]!.GetValue<string>() == "RESULTADO_FINAL")!["tiposDeDecisao"]!);

    private static string[] Textos(JsonNode lista) => [.. lista.AsArray().Select(static tipo => tipo!.GetValue<string>())];

    private static void DefinirTipos(JsonObject envelope, string caminho, params string[] tipos) =>
        NoDe(envelope, caminho)["tiposDeDecisao"] = new JsonArray([.. tipos.Select(static tipo => (JsonNode)JsonValue.Create(tipo)!)]);

    private static void AusentarOuAnular(JsonObject no, bool nula)
    {
        if (nula)
        {
            no["tiposDeDecisao"] = null;
            return;
        }

        no.Remove("tiposDeDecisao");
    }

    private static JsonObject NoDe(JsonObject envelope, string caminho)
    {
        JsonNode atual = envelope;
        foreach (string parte in caminho.Split('.'))
        {
            atual = int.TryParse(parte, CultureInfo.InvariantCulture, out int indice) ? atual.AsArray()[indice]! : atual[parte]!;
        }

        return atual.AsObject();
    }
}
