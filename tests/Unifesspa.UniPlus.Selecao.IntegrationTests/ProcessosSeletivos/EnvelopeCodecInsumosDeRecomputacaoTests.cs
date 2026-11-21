namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

public sealed class EnvelopeCodecInsumosDeRecomputacaoTests
{

    /// <summary>
    /// <c>vagasOfertadasBase</c>, <c>percentualDaReservaLegal</c> e a referência demográfica
    /// (issue #848/ADR-0115) não são campos-folha: são insumo do quadro de vagas, que
    /// <see cref="Domain.Entities.ConfiguracaoDistribuicaoVagas.Criar"/> recalcula do zero a cada
    /// reidratação (a prova não-circular exige recomputar do insumo, não reler o quadro congelado).
    /// Mutar um insumo sem mutar o quadro deixa o congelado diferente do que a declaração monta, e a
    /// leitura o recusa — o que só acontece se ela LÊ o insumo e o usa na montagem.
    /// </summary>
    [Theory(DisplayName = "O decoder lê os insumos do quadro de vagas e recusa o envelope cujo quadro congelado não os reflete")]
    [InlineData("distribuicao.0.vagasOfertadasBase", "77")]
    [InlineData("distribuicao.0.percentualDaReservaLegal", "0.9000")]
    [InlineData("distribuicao.0.referenciaDemografica.ppiPercentual", "12.34")]
    public void Decoder_LeOsInsumosDoQuadro_ERecusaOQuadroQueNaoOsReflete(string caminho, string valorNovo)
    {
        ArgumentNullException.ThrowIfNull(caminho);

        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);
        JsonObject mutado = Envelope(congelado);
        Mutar(mutado, caminho, valorNovo);

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, PerfilCanonicoV1.Instancia.Serializar(mutado));
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);

        reidratado.IsFailure.Should().BeTrue($"o quadro congelado não é o que '{caminho}' monta");
        reidratado.Error!.Message.Should().Contain("o quadro congelado não é o que a declaração da oferta monta");
    }

    // ── Cultura: os decimais do envelope são InvariantCulture, sempre ──

    /// <summary>
    /// Mesma razão da banca, um bloco ao lado: o recurso da etapa é declarado pelo evento que inicia o
    /// prazo, e o comando não carrega o id da linha — regravar a etapa constrói um período recursal novo.
    /// </summary>
    [Fact(DisplayName = "Trocar o id do recurso da etapa não muda os bytes canônicos")]
    public void IdDoRecursoDaEtapa_NaoEntraNosBytes()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;

        EtapaProcesso objetiva = processo.Etapas.Single(e => e.Nome == "Prova Objetiva");
        IReadOnlyList<RecursoDaEtapa> originais = [.. objetiva.Recursos];
        originais.Should().NotBeEmpty("pré-condição: a etapa do corpus abre janela recursal");
        RegravarOsRecursosComIdNovo(objetiva, originais);
        objetiva.Recursos.Select(r => r.Id)
            .Should().NotIntersectWith(originais.Select(r => r.Id), "pré-condição: as janelas reconstruídas têm identidade nova");

        byte[] depois = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;

        depois.Should().Equal(antes, "a mesma janela recursal, numa linha nova, é a mesma publicação");
    }

    private static void RegravarOsRecursosComIdNovo(EtapaProcesso etapa, IReadOnlyList<RecursoDaEtapa> originais) =>
        etapa.DefinirRecursos([.. originais.Select(r => RecursoDaEtapa.Reidratar(
            Guid.CreateVersion7(), r.Ancora, r.Regra, ParametrosDoRecursoDeTeste.DeclaradosDe(r.Parametros), r.ProdutoAncoraId, r.Procedimentos).Value!)])
            .IsSuccess.Should().BeTrue();

    /// <summary>
    /// A banca da etapa é declarada pelo TIPO, não pela linha: regravar a etapa sem mudar nada
    /// constrói uma banca nova, com id novo. Congelar esse id fazia o hash da publicação mudar
    /// depois de um PUT que não mudou configuração alguma — e o hash é justamente o que prova
    /// que a publicação continua sendo a mesma.
    /// </summary>
    [Fact(DisplayName = "Redeclarar a mesma banca da etapa preserva a linha e os bytes canônicos")]
    public void RedeclararAMesmaBancaDaEtapa_PreservaLinhaEBytes()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;
        EtapaProcesso objetiva = processo.Etapas.Single(e => e.Nome == "Prova Objetiva");
        Dictionary<string, Guid> idsOriginais = IdsDasBancasPorCodigo(objetiva);

        RedeclararComOAcentoNaFormaComposta(objetiva);

        ConferirQueOCodigoGuardadoEOOriginal(objetiva, idsOriginais);
        ConferirQueALinhaNaoGirouDeId(objetiva, idsOriginais);
        byte[] depois = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;
        depois.Should().Equal(antes, "a mesma configuração de banca é a mesma publicação");
    }

    private static Dictionary<string, Guid> IdsDasBancasPorCodigo(EtapaProcesso etapa)
    {
        Dictionary<string, Guid> ids = etapa.Bancas.ToDictionary(b => b.Codigo, b => b.Id, StringComparer.Ordinal);
        ids.Should().HaveCountGreaterThan(1, "pré-condição: a etapa do corpus requer mais de uma banca");
        ids.Values.Should().NotContain(Guid.Empty);
        return ids;
    }

    // O cliente devolve a coleção inteira que leu, mas com o acento na forma COMPOSTA — é o
    // que um navegador ou um sistema operacional que normaliza diferente do que gravou
    // enviaria de volta. Devolver a cópia literal do que está guardado não provaria nada:
    // decomposto contra decomposto casa até numa comparação ordinal, e uma regressão do
    // reencontro para igualdade de bytes passaria batida. Com as formas trocadas, só casa
    // quem normaliza — e quem não normalizar recria a linha, girando o id.
    private static void RedeclararComOAcentoNaFormaComposta(EtapaProcesso etapa) =>
        etapa.DefinirBancas([.. etapa.Bancas.Select(b =>
            BancaDaEtapa.Criar(b.TipoBancaOrigemId, b.Codigo.Normalize(NormalizationForm.FormC), b.Nome, b.RegraDeDeliberacao))])
            .IsSuccess.Should().BeTrue();

    // O que fica guardado é o código da linha PRESERVADA — a forma decomposta original —, e
    // não a composta que o cliente acabou de mandar. É o desfecho certo: reconciliar
    // preserva a linha, e trocar o texto dela por um canonicamente equivalente seria uma
    // escrita sem mudança de conteúdo. Afirmar isto é o que prende as duas metades: o
    // reencontro aconteceu apesar das formas diferentes, e a linha não foi tocada.
    private static void ConferirQueOCodigoGuardadoEOOriginal(EtapaProcesso etapa, Dictionary<string, Guid> idsOriginais) =>
        etapa.Bancas.Select(b => b.Codigo).Should().BeEquivalentTo(idsOriginais.Keys,
            "a redeclaração reencontrou as linhas existentes em vez de criar outras, e o texto "
            + "delas é o que já estava guardado");

    private static void ConferirQueALinhaNaoGirouDeId(EtapaProcesso etapa, Dictionary<string, Guid> idsOriginais)
    {
        foreach (BancaDaEtapa banca in etapa.Bancas)
        {
            banca.Id.Should().Be(idsOriginais[banca.Codigo],
                "o código não mudou, e recriar a linha giraria o id — o que basta para o hash da "
                + "publicação mudar depois de uma gravação que não mudou configuração alguma");
        }
    }

    // ── Golden fixture RICA: o decoder é ancorado num artefato congelado ──

    [Fact(DisplayName = "Golden rica — a fixture é o envelope do corpus (regeneração explícita via UPDATE_ENVELOPE_FIXTURE=1)")]
    public void GoldenRica_EOEnvelopeDoCorpus()
    {
        SnapshotCanonico atual = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(CorpusEnvelopeProcessoRico.ProcessoRico()));

        if (Environment.GetEnvironmentVariable("UPDATE_ENVELOPE_FIXTURE") == "1")
        {
            string destino = CaminhoNoFonte();
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            File.WriteAllBytes(destino, atual.Bytes);
        }

        atual.Bytes.Should().Equal(LerFixtureRica(),
            "o envelope do corpus mudou sem que a fixture fosse regenerada. Se a mudança é intencional, rode " +
            "UPDATE_ENVELOPE_FIXTURE=1 e leve o diff da fixture para a revisão — que é todo o ponto dela.");
    }

    private static byte[] LerFixtureRica() => File.ReadAllBytes(Path.Join(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "ProcessosSeletivos",
        "Fixtures",
        "envelope-rico.json"));

    private static string CaminhoNoFonte([CallerFilePath] string origem = "") => Path.Join(
        Path.GetDirectoryName(origem)!,
        "Fixtures",
        "envelope-rico.json");

    // ── Round-trip com exigência documental rica (Story #554, PR #903; Story #919, congelamento dos parâmetros na publicação, UNI-REQ-0019) ──
}
