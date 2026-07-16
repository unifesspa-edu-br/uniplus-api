namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// <b>A prova de fidelidade da reidratação</b> (Story #859, ADR-0110 D1/D2/D8).
/// </summary>
/// <remarks>
/// <para>
/// Esta é a suíte de risco da Feature. Todo o resto dela é máquina de estados; aqui um
/// agregado é reconstruído a partir de bytes com peso jurídico. <b>Um campo perdido não
/// aparece em lugar nenhum</b>: o descarte de uma sessão editorial repõe a configuração
/// sem ele, e o certame publicado passa a divergir do documento que o publicou — sem
/// erro, sem log, sem ninguém ver.
/// </para>
/// <para>
/// A projeção é pura (ADR-0109 D6), então nada aqui precisa de banco. O que precisa de
/// banco — o identity map, as FKs, o cascade — está em
/// <c>RestaurarConfiguracaoPersistenciaTests</c>.
/// </para>
/// </remarks>
public sealed class EnvelopeCodecRoundTripTests
{
    // ── CA-01 — round-trip byte-a-byte, com o encoder DA VERSÃO ──

    [Fact(DisplayName = "CA-01 — reidratar a versão 1 e recanonicalizá-la reproduz os bytes congelados, inteiros")]
    public void RoundTrip_VersaoDeAbertura()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        AssertRoundTrip(processo, versao, congelado);
    }

    [Fact(DisplayName = "CA-01 — a versão N>1 tem o 18º bloco: o round-trip usa a RetificacaoInfo ORIGINAL, recuperada do próprio envelope")]
    public void RoundTrip_VersaoRetificada()
    {
        // O round-trip de uma versão retificada NÃO é "os 17 blocos". Ela tem o 18º
        // (`retificacao`), que não vem do agregado — é parâmetro externo da
        // canonicalização. Recanonicalizar sem ele produziria um envelope de 17 blocos
        // e a comparação falharia por uma razão que nada tem a ver com a reidratação.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        RetificacaoInfo retificacao = new(CorpusEnvelope.AtoAbertura, "Correção do quadro de vagas do curso de Direito");

        SnapshotCanonico abertura = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo, retificacao));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao v1 = CorpusEnvelope.VersaoDeAbertura(processo, abertura.Bytes);
        VersaoConfiguracao v2 = CorpusEnvelope.VersaoDeRetificacao(v1, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, v2, congelado);

        envelope.Retificacao.Should().NotBeNull("a versão N>1 carrega o 18º bloco");
        envelope.Retificacao!.EditalRetificadoId.Should().Be(CorpusEnvelope.AtoAbertura);
        envelope.Retificacao.Motivo.Should().Be("Correção do quadro de vagas do curso de Direito");
    }

    [Fact(DisplayName = "CA-01 — o round-trip parte de uma configuração viva DIFERENTE da congelada (é o que o descarte faz)")]
    public void RoundTrip_SobreConfiguracaoViva_Divergente()
    {
        // Restaurar sobre uma configuração viva IDÊNTICA à congelada esconderia metade do
        // risco: as etapas seriam reconciliadas nos mesmos Ids e as demais dimensões
        // trocadas por instâncias equivalentes. É a divergência que exercita o caminho
        // real do descarte — repor o que a sessão editorial havia substituído.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        // A "sessão editorial": a configuração viva vira outra coisa.
        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();
        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().NotEqual(congelado.Bytes, "pré-condição: a configuração viva TEM de estar divergente da congelada");

        AssertRoundTrip(processo, versao, congelado);
    }

    [Fact(DisplayName = "CA-13/CA-20 — o round-trip preserva obrigatoriedades[] com Conformidade não vazia, ordenada por RegraId mesmo com regras fora de ordem na entrada")]
    public void RoundTrip_ComConformidadeLegalCongelada_PreservaObrigatoriedadesOrdenadasPorRegraId()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();

        RegraAvaliada regraMaisRecente = new(
            RegraId: new Guid("aaaaaaaa-0000-7000-8000-000000000002"),
            RegraCodigo: "REGRA-B",
            Categoria: CategoriaObrigatoriedade.Outros,
            TipoProcessoCodigoAvaliado: "SiSU",
            Predicado: new EtapaObrigatoria("Prova Objetiva"),
            Aprovada: true,
            Motivo: null,
            BaseLegal: "Lei de teste B",
            AtoNormativoUrl: null,
            PortariaInterna: null,
            DescricaoHumana: "Regra B",
            VigenciaInicio: new DateOnly(2020, 1, 1),
            VigenciaFim: null,
            Hash: new string('b', 64));

        RegraAvaliada regraMaisAntiga = new(
            RegraId: new Guid("aaaaaaaa-0000-7000-8000-000000000001"),
            RegraCodigo: "REGRA-A",
            Categoria: CategoriaObrigatoriedade.Outros,
            TipoProcessoCodigoAvaliado: "SiSU",
            Predicado: new ConcorrenciaDuplaObrigatoria(),
            Aprovada: true,
            Motivo: null,
            BaseLegal: "Lei de teste A",
            AtoNormativoUrl: "https://example.org/ato",
            PortariaInterna: "PORT-001",
            DescricaoHumana: "Regra A",
            VigenciaInicio: new DateOnly(2019, 1, 1),
            VigenciaFim: new DateOnly(2030, 1, 1),
            Hash: new string('a', 64));

        // Deliberadamente fora de ordem: regraMaisRecente (id maior) vem PRIMEIRO na lista de
        // entrada — se o encoder não ordenasse por RegraId, o array congelado sairia nesta
        // mesma ordem "errada", e esta regressão (round-trip perdendo Conformidade no
        // restaurador) não seria pega por nenhum outro teste desta suíte, que nunca
        // exercitava uma Conformidade não vazia.
        ResultadoConformidade conformidade = new([regraMaisRecente, regraMaisAntiga], []);

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo, conformidade: conformidade));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, versao, congelado);

        envelope.Conformidade.Should().NotBeNull(
            "a fonte da regressão: o restaurador tem de repassar Conformidade adiante na recanonicalização");
        envelope.Conformidade!.Regras.Select(r => r.RegraCodigo).Should().Equal(["REGRA-A", "REGRA-B"],
            "o encoder ordena obrigatoriedades[] por RegraId, ascendente, mesmo que a entrada não venha ordenada (CA-13)");

        JsonArray obrigatoriedadesJson = Envelope(congelado)["documentosExigidos"]!["obrigatoriedades"]!.AsArray();
        obrigatoriedadesJson.Select(o => o!["regraCodigo"]!.GetValue<string>()).Should().Equal(["REGRA-A", "REGRA-B"]);
    }

    /// <summary>
    /// Reidrata, repõe e recanonicaliza <b>com o encoder da versão dela</b> — nunca com o
    /// corrente. É o que torna a prova não-circular: no dia da <c>1.2</c>, recanonicalizar
    /// uma <c>1.1</c> com o encoder corrente produziria bytes de <c>1.2</c>, e a fidelidade
    /// da reidratação de tudo o que já foi publicado deixaria de ser verificável.
    /// </summary>
    private static EnvelopeReidratado AssertRoundTrip(
        ProcessoSeletivo processo,
        VersaoConfiguracao versao,
        SnapshotCanonico congelado)
    {
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        EnvelopeReidratado envelope = reidratado.Value!;

        Result restauracao = processo.RestaurarConfiguracaoCongelada(versao, envelope.Grafo);
        restauracao.IsSuccess.Should().BeTrue(restauracao.Error?.Message);

        Result<SnapshotCanonico> recodificado = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                processo, envelope.Dados, envelope.HashDocumento, envelope.Retificacao, envelope.Conformidade));
        recodificado.IsSuccess.Should().BeTrue(recodificado.Error?.Message);

        // Os três são independentes no modelo (VersaoConfiguracao guarda schema_version e
        // algoritmo em colunas próprias). Comparar só os bytes deixaria passar uma versão
        // reidratada que declarasse outra forma ou outro algoritmo de hash.
        recodificado.Value!.Bytes.Should().Equal(congelado.Bytes,
            "reidratar e recanonicalizar tem de reproduzir os bytes congelados INTEIROS — qualquer campo que o " +
            "decoder perca sai daqui como uma divergência de bytes, e é a única forma de vê-lo");
        recodificado.Value.SchemaVersion.Should().Be(congelado.SchemaVersion);
        recodificado.Value.AlgoritmoHash.Should().Be(congelado.AlgoritmoHash);

        return envelope;
    }

    // ── CA-03 — o etapa.Id é PRESERVADO (asserção direta sobre o decoder) ──

    [Fact(DisplayName = "CA-03 — o decoder preserva o etapa.Id congelado; regenerá-lo faz o etapaRef deixar de resolver")]
    public void Decoder_PreservaIdDaEtapa()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        EnvelopeReidratado envelope = CorpusEnvelope.Registro.Reidratar(versao).Value!;

        // Asserção DIRETA sobre o decoder: os ids que ele devolveu são os do JSON. Provar
        // isso só pelo round-trip seria indireto — e construir o grafo à mão com um id
        // regenerado testaria a guarda do agregado, não o decoder.
        JsonArray etapasJson = Envelope(congelado)["etapas"]!.AsArray();
        IEnumerable<Guid> idsNoJson = etapasJson.Select(e => Guid.Parse(e!["id"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        IEnumerable<Guid> idsReidratados = envelope.Grafo.Etapas.Select(e => e.Id);

        idsReidratados.Should().BeEquivalentTo(idsNoJson,
            "o etapa.Id é o ÚNICO id de filha que o envelope congela, porque criteriosDesempate.args.etapaRef e " +
            "regrasEliminacao.args.etapaRef apontam para ele (ADR-0110 D2)");

        // E a contraprova: regenerar o id quebra o round-trip — o etapaRef fica órfão.
        GrafoConfiguracao comIdRegenerado = new(
            etapas: [.. envelope.Grafo.Etapas.Select(e =>
                EtapaProcesso.Criar(e.Nome, e.Carater, e.Peso, e.NotaMinima, e.Ordem))],
            ofertaAtendimento: envelope.Grafo.OfertaAtendimento,
            distribuicaoVagas: envelope.Grafo.DistribuicaoVagas,
            bonusRegional: envelope.Grafo.BonusRegional,
            criteriosDesempate: envelope.Grafo.CriteriosDesempate,
            classificacao: envelope.Grafo.Classificacao,
            cronogramaFases: envelope.Grafo.CronogramaFases);

        Result recusa = processo.RestaurarConfiguracaoCongelada(versao, comIdRegenerado);

        recusa.IsFailure.Should().BeTrue(
            "regenerar o etapa.Id deixa o etapaRef do desempate e da eliminação apontando para etapas que não " +
            "existem mais — o certame ficaria com desempate e eliminação inexecutáveis");
        recusa.Error!.Code.Should().BeOneOf(
            "ProcessoSeletivo.EtapaRefDesempateInexistente",
            "ProcessoSeletivo.EtapaRefEliminacaoInexistente");
    }

    // ── CA-02 — o decoder LÊ cada campo (matriz fechada de paths) ──

    /// <summary>
    /// Cada linha muta <b>um</b> valor no envelope congelado e exige que a reidratação o
    /// carregue até os bytes recodificados.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>É a contraprova que prova o decoder, não o encoder.</b> Um teste que apenas
    /// alterasse o agregado e visse os bytes mudarem provaria que o <i>encoder</i> escreve
    /// o campo — coisa que já sabemos. Aqui a mutação é feita <b>no JSON</b>: se o decoder
    /// ignorar o campo (ou cair num default), a recodificação devolve o valor
    /// <b>original</b> e a asserção falha. Um decoder que perca <c>modalidade.baseLegal</c>
    /// é pego exatamente aqui.
    /// </para>
    /// <para>
    /// <b>A asserção primária é a desigualdade com o envelope ORIGINAL</b>, e é
    /// deliberadamente essa: ela é imune à reordenação. <c>ComputeSnapshotBytes</c> ordena
    /// chaves de objeto mas <b>preserva a ordem dos arrays</b>, ao passo que o encoder
    /// ordena etapas, distribuições, modalidades, critérios e eliminações por regras
    /// próprias (ADR-0109 D9). Comparar contra o “JSON mutado no lugar” reprovaria uma
    /// implementação <b>correta</b> sempre que a mutação tocasse uma chave de ordenação.
    /// A desigualdade com o original não tem esse problema e prova exatamente o que
    /// importa: <b>se o decoder ignorasse o campo, a recodificação traria o valor
    /// original de volta</b> — e os bytes bateriam com os originais.
    /// </para>
    /// <para>
    /// A igualdade com o JSON mutado é a asserção <b>secundária</b>, mais forte, e só vale
    /// enquanto os paths abaixo ficarem <b>fora das chaves de ordenação</b> (o que é o caso:
    /// nenhum deles muta <c>ordem</c>, <c>id</c>, <c>codigo</c>, <c>ofertaCursoOrigemId</c>
    /// nem o interior de <c>regrasEliminacao</c>, que ordena por chave de conteúdo). Os
    /// campos que <b>são</b> chave de ordenação estão no teste seguinte, com a asserção
    /// primária apenas.
    /// </para>
    /// </remarks>
    [Theory(DisplayName = "CA-02 — o decoder não perde campo: mutar o JSON muda os bytes reidratados")]
    [InlineData("etapas.0.nome", "Prova Objetiva Reformulada")]
    [InlineData("etapas.0.peso", "9.8750")]
    [InlineData("etapas.0.notaMinima", "12.3400")]
    [InlineData("etapas.0.carater", "Classificatoria")]
    [InlineData("periodo.numero", "099/2026")]
    [InlineData("periodo.inicio", "2026-03-09")]
    [InlineData("periodo.fim", "2026-04-30")]
    [InlineData("distribuicao.0.regraDistribuicao.versao", "v9")]
    [InlineData("distribuicao.0.referenciaDemografica.censoReferencia", "Censo IBGE 2010")]
    [InlineData("distribuicao.0.referenciaDemografica.baseLegal", "Lei 14.723/2023 art. 3º")]
    [InlineData("modalidades.0.baseLegal", "Outra base legal inteiramente diversa")]
    [InlineData("modalidades.0.descricao", "Outra descrição")]
    [InlineData("modalidades.1.acaoQuandoIndeferido", "RECLASSIFICAR_REGRA_EDITAL")]
    [InlineData("modalidades.1.criteriosCumulativos.0", "renda_per_capita_ate_meio_sm")]
    [InlineData("bonusRegional.fator", "1.9900")]
    [InlineData("bonusRegional.teto", "42.5000")]
    [InlineData("bonusRegional.municipioConvenio", "Parauapebas")]
    [InlineData("bonusRegional.baseLegal", "Res. Unifesspa 999/2026")]
    [InlineData("classificacao.nOpcoesAlocacao", "1")]
    [InlineData("classificacao.casasArredondamento", "4")]
    [InlineData("classificacao.regraCalculo.hash", "9999999999999999999999999999999999999999999999999999999999999999")]
    [InlineData("atendimento.condicoes.0.condicaoNome", "Outra condição")]
    [InlineData("atendimento.recursos.0.recursoNome", "Intérprete de Libras")]
    [InlineData("atendimento.tiposDeficiencia.0.tipoDeficienciaNome", "Deficiência física")]
    public void Decoder_NaoPerdeCampo(string caminho, string valorNovo)
    {
        (byte[] originais, byte[] mutados, byte[] recodificados) = MutarEReidratar(caminho, valorNovo);

        recodificados.Should().NotEqual(originais,
            $"o decoder tem de LER '{caminho}'. Se ele o ignorasse, a recodificação traria o valor ORIGINAL de volta " +
            "— e o campo teria sido perdido em silêncio, que é exatamente como um descarte destrói configuração.");

        recodificados.Should().Equal(mutados,
            $"além de ler '{caminho}', o decoder tem de reconstruir todo o resto fielmente — os bytes reidratados " +
            "reproduzem o envelope mutado inteiro");
    }

    /// <summary>
    /// Os campos que <b>são chave de ordenação</b> do encoder — mutá-los reposiciona o item
    /// no array recodificado (ADR-0109 D9). Aqui só cabe a asserção primária: comparar com
    /// o “JSON mutado no lugar” reprovaria a implementação correta.
    /// </summary>
    [Theory(DisplayName = "CA-02 — o decoder lê também os campos que são chave de ordenação")]
    [InlineData("etapas.0.ordem", "9")]
    [InlineData("criteriosDesempate.0.ordem", "9")]
    [InlineData("classificacao.regrasEliminacao.0.args.notaMinima", "88.7500")]
    [InlineData("classificacao.regrasEliminacao.2.args.minimo", "555.0000")]
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
    [Theory(DisplayName = "CA-02 — o decoder lê também os insumos que disparam recomputação do quadro de vagas")]
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
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        JsonObject mutado = Envelope(congelado);
        Mutar(mutado, caminho, valorNovo);

        byte[] bytesMutados = HashCanonicalComputer.ComputeSnapshotBytes(mutado);
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
                reidratado.Value.HashDocumento,
                reidratado.Value.Retificacao,
                reidratado.Value.Conformidade)).Value!.Bytes;

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
                reidratado.Value.HashDocumento,
                reidratado.Value.Retificacao,
                reidratado.Value.Conformidade)).Value!.Bytes;

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
        "envelope-1.1-rico.json"));

    private static string CaminhoNoFonte([CallerFilePath] string origem = "") => Path.Join(
        Path.GetDirectoryName(origem)!,
        "Fixtures",
        "envelope-1.1-rico.json");

    internal static JsonObject Envelope(SnapshotCanonico snapshot) =>
        JsonNode.Parse(Encoding.UTF8.GetString(snapshot.Bytes))!.AsObject();

    /// <summary>
    /// Troca <b>um</b> valor num path <c>a.0.b</c>, preservando o <b>tipo JSON</b> do
    /// original — o envelope escreve decimais como string e inteiros como número, e trocar
    /// um pelo outro testaria o parser de tipos, não a leitura do campo.
    /// </summary>
    private static void Mutar(JsonObject raiz, string caminho, string valorNovo)
    {
        string[] partes = caminho.Split('.');
        JsonNode atual = raiz;

        for (int i = 0; i < partes.Length - 1; i++)
        {
            atual = int.TryParse(partes[i], CultureInfo.InvariantCulture, out int indice)
                ? atual.AsArray()[indice]!
                : atual.AsObject()[partes[i]]!;
        }

        string chave = partes[^1];

        if (int.TryParse(chave, CultureInfo.InvariantCulture, out int posicao))
        {
            JsonArray array = atual.AsArray();
            array[posicao].Should().NotBeNull($"pré-condição: o path '{caminho}' tem de existir no envelope");
            array[posicao] = ValorComoNo(array[posicao]!, valorNovo);
            return;
        }

        JsonObject objeto = atual.AsObject();
        objeto[chave].Should().NotBeNull($"pré-condição: o path '{caminho}' tem de existir no envelope");
        objeto[chave] = ValorComoNo(objeto[chave]!, valorNovo);
    }

    private static JsonValue ValorComoNo(JsonNode original, string valor) =>
        original.GetValueKind() == System.Text.Json.JsonValueKind.Number
            ? JsonValue.Create(int.Parse(valor, CultureInfo.InvariantCulture))
            : JsonValue.Create(valor);
}
