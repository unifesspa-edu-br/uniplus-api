namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
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

public sealed partial class EnvelopeCodecRoundTripTests
{
    /// <summary>
    /// Os campos que <b>são chave de ordenação</b> do encoder — mutá-los reposiciona o item
    /// no array recodificado (ADR-0109 D9). Aqui só cabe a asserção primária: comparar com
    /// o “JSON mutado no lugar” reprovaria a implementação correta.
    /// </summary>
    /// <remarks>
    /// <c>modalidades.1.regraDeAplicabilidade.0.0.valor</c> entrou aqui pela mesma razão de
    /// <c>regrasEliminacao</c> (issue #1067): as cláusulas da regra são ordenadas pela chave de
    /// conteúdo, e mutar o valor de uma condição pode mudar a posição da cláusula entre as duas —
    /// a modalidade <c>LB_EP</c> (índice 1 do array) tem exatamente duas cláusulas
    /// (<see cref="CorpusEnvelope.ProcessoRico"/>), o mínimo para a reordenação ser observável.
    /// </remarks>
    [Theory(DisplayName = "O decoder lê também os campos que são chave de ordenação")]
    [InlineData("etapas.0.ordem", "9")]
    [InlineData("criteriosDesempate.0.ordem", "9")]
    [InlineData("classificacao.regrasEliminacao.1.args.notaMinima", "88.7500")]
    [InlineData("classificacao.regrasEliminacao.0.args.minimo", "555.0000")]
    [InlineData("classificacao.regrasEliminacao.0.args.areaCodigo", "MATEMATICA")]
    [InlineData("modalidades.1.regraDeAplicabilidade.0.0.valor|modalidades.10.regraDeAplicabilidade.0.0.valor", "ATE_MEIO_SM")]
    public void Decoder_NaoPerdeCampoDeOrdenacao(string caminho, string valorNovo)
    {
        (byte[] originais, byte[] _, byte[] recodificados) = MutarEReidratar(caminho, valorNovo);

        recodificados.Should().NotEqual(originais,
            $"o decoder tem de LER '{caminho}' — se o ignorasse, a recodificação traria o valor original de volta");
    }

    /// <summary>
    /// <c>voBase</c> e <c>pr</c> (issue #848/ADR-0115) não são mais campos-folha: são
    /// insumo do quadro de vagas, que <see cref="Domain.Entities.ConfiguracaoDistribuicaoVagas.Criar"/>
    /// recalcula do zero a cada reidratação (a prova não-circular do CA-13 exige
    /// exatamente isso — recomputar do insumo, não reler o output congelado). Mutar
    /// qualquer um dos dois muda o bloco <c>vagas</c> recodificado de um jeito que o
    /// "JSON mutado no lugar" não prevê (ele só tem o insumo alterado, não o quadro
    /// recomputado a partir dele) — por isso, como os campos de ordenação, só cabe a
    /// asserção primária.
    /// </summary>
    [Theory(DisplayName = "O decoder lê também os insumos que disparam recomputação do quadro de vagas")]
    [InlineData("distribuicao.0.voBase", "77")]
    [InlineData("distribuicao.0.pr", "0.9000")]
    [InlineData("distribuicao.0.referenciaDemografica.ppiPercentual", "12.34")]
    public void Decoder_NaoPerdeCampoQueDisparaRecomputacaoDoQuadro(string caminho, string valorNovo)
    {
        (byte[] originais, byte[] _, byte[] recodificados) = MutarEReidratar(caminho, valorNovo);

        recodificados.Should().NotEqual(originais,
            $"o decoder tem de LER '{caminho}' — se o ignorasse, a recodificação traria o valor original de volta");
    }

    private static (byte[] Originais, byte[] Mutados, byte[] Recodificados) MutarEReidratar(string caminho, string valorNovo)
    {
        ArgumentNullException.ThrowIfNull(caminho);

        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        // A mesma modalidade é ofertada pelos dois cursos do processo rico, e o processo não aceita
        // duas cópias diferentes dela: o caminho pode nomear as duas ocorrências, separadas por '|'.
        JsonObject mutado = Envelope(congelado);
        foreach (string ocorrencia in caminho.Split('|'))
        {
            Mutar(mutado, ocorrencia, valorNovo);
        }

        byte[] bytesMutados = PerfilCanonicoV1.Instancia.Serializar(mutado);
        bytesMutados.Should().NotEqual(congelado.Bytes, $"pré-condição: mutar '{caminho}' tem de mudar os bytes");

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, bytesMutados);
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        Result reposicao = processo.RestaurarConfiguracaoCongelada(versao, reidratado.Value!.Grafo);
        reposicao.IsSuccess.Should().BeTrue(reposicao.Error?.Message);

        byte[] recodificados = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                processo,
                reidratado.Value.Dados,
                reidratado.Value.HashDocumento, FusoInstitucional.ZoneId,
                reidratado.Value.Retificacao,
                reidratado.Value.Conformidade,
                reidratado.Value.MetadadosFatosCongelados,
                reidratado.Value.ValoresSelecionaveisCongelados,
                reidratado.Value.CalendarioDiasUteis, reidratado.Value.AgregadosDosGrupos, reidratado.Value.ApresentacaoDosPressupostos,
                TiposDeAtoCongelados: reidratado.Value.TiposDeAtoCongelados, FatosDerivadosCongelados: reidratado.Value.FatosDerivadosCongelados)).Value!.Bytes;

        return (congelado.Bytes, bytesMutados, recodificados);
    }

    // ── Cultura: os decimais do envelope são InvariantCulture, sempre ──

    [Fact(DisplayName = "O round-trip é imune à cultura do host — pt-BR usa vírgula decimal")]
    public void RoundTrip_ImuneACultura()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            RoundTrip_VersaoDeAbertura();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// Mesma razão da banca, um bloco ao lado: o recurso da etapa é declarado pela ÂNCORA, e o
    /// comando não carrega o id da linha — regravar a etapa constrói uma janela recursal nova.
    /// </summary>
    [Fact(DisplayName = "Trocar o id do recurso da etapa não muda os bytes canônicos")]
    public void IdDoRecursoDaEtapa_NaoEntraNosBytes()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        EtapaProcesso objetiva = processo.Etapas.Single(e => e.Nome == "Prova Objetiva");
        IReadOnlyList<RecursoDaEtapa> originais = [.. objetiva.Recursos];
        originais.Should().NotBeEmpty("pré-condição: a etapa do corpus abre janela recursal");

        objetiva.DefinirRecursos([.. originais.Select(r => RecursoDaEtapa.Reidratar(
            Guid.CreateVersion7(), r.Ancora, r.Regra, r.Args, r.ProdutoAncoraId).Value!)])
            .IsSuccess.Should().BeTrue();
        objetiva.Recursos.Select(r => r.Id)
            .Should().NotIntersectWith(originais.Select(r => r.Id),
                "pré-condição: as janelas reconstruídas têm identidade nova");

        byte[] depois = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        depois.Should().Equal(antes,
            "a mesma janela recursal, numa linha nova, é a mesma publicação");
    }

    /// <summary>
    /// A banca da etapa é declarada pelo TIPO, não pela linha: regravar a etapa sem mudar nada
    /// constrói uma banca nova, com id novo. Congelar esse id fazia o hash da publicação mudar
    /// depois de um PUT que não mudou configuração alguma — e o hash é justamente o que prova
    /// que a publicação continua sendo a mesma.
    /// </summary>
    [Fact(DisplayName = "Redeclarar a mesma banca da etapa preserva a linha e os bytes canônicos")]
    public void RedeclararAMesmaBancaDaEtapa_PreservaLinhaEBytes()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        EtapaProcesso objetiva = processo.Etapas.Single(e => e.Nome == "Prova Objetiva");
        Dictionary<string, Guid> idsOriginais = IdsDasBancasPorCodigo(objetiva);

        RedeclararComOAcentoNaFormaComposta(objetiva);

        ConferirQueOCodigoGuardadoEOOriginal(objetiva, idsOriginais);
        ConferirQueALinhaNaoGirouDeId(objetiva, idsOriginais);
        byte[] depois = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
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
            BancaDaEtapa.Criar(b.TipoBancaOrigemId, b.Codigo.Normalize(NormalizationForm.FormC), b.Nome))])
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

    [Fact(DisplayName = "Golden rica — a fixture congelada no repositório reidrata e recanonicaliza byte-a-byte")]
    public void GoldenRica_ReidrataEBate()
    {
        // Um corpus gerado pelo encoder do dia e consumido pelo decoder do dia não prova
        // compatibilidade: os dois derivariam juntos e o teste continuaria verde. A fixture
        // é o artefato CONGELADO — se o encoder mudar sem bump de versão, é aqui que se vê.
        byte[] fixture = LerFixtureRica();

        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao v1 = CorpusEnvelope.VersaoDeAbertura(processo, fixture);

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(v1);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        processo.RestaurarConfiguracaoCongelada(v1, reidratado.Value!.Grafo).IsSuccess.Should().BeTrue();

        byte[] recodificado = CorpusEnvelope.Registro.Recodificar(
            v1.SchemaVersion,
            new EntradaCanonicalizacao(
                processo,
                reidratado.Value.Dados,
                reidratado.Value.HashDocumento, FusoInstitucional.ZoneId,
                reidratado.Value.Retificacao,
                reidratado.Value.Conformidade,
                reidratado.Value.MetadadosFatosCongelados,
                reidratado.Value.ValoresSelecionaveisCongelados,
                reidratado.Value.CalendarioDiasUteis, reidratado.Value.AgregadosDosGrupos, reidratado.Value.ApresentacaoDosPressupostos,
                TiposDeAtoCongelados: reidratado.Value.TiposDeAtoCongelados, FatosDerivadosCongelados: reidratado.Value.FatosDerivadosCongelados)).Value!.Bytes;

        recodificado.Should().Equal(fixture,
            "a fixture rica é o oráculo do decoder — bytes reais, GUIDs reais, agregado completo. Se ela deixar de " +
            "reidratar, o envelope mudou de forma sem bump de versão, e o descarte de tudo o que já foi publicado " +
            "deixou de ser verificável.");
    }

    [Fact(DisplayName = "Golden rica — a fixture é o envelope do corpus (regeneração explícita via UPDATE_ENVELOPE_FIXTURE=1)")]
    public void GoldenRica_EOEnvelopeDoCorpus()
    {
        SnapshotCanonico atual = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(CorpusEnvelope.ProcessoRico()));

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
