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
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// <b>A prova de fidelidade da reidratação</b> (Story #859, ADR-0110): reidratar um envelope
/// congelado e recanonicalizá-lo reproduz os mesmos bytes, byte a byte, incluindo a preservação
/// de identidade (etapa.Id). Prova o codec CORRENTE — no regime pré-produção (<c>EnvelopeCodec</c>,
/// codec único, forma reescrita no lugar a cada bump), um bump de <c>schema_version</c> troca o
/// codec corrente sem preservar o anterior no registro; o versionamento forense por codec
/// (um por <c>schema_version</c>, nenhum jamais aposentado) só passa a valer a partir do primeiro
/// certame publicado em qualquer ambiente, inclusive homologação — não da primeira release de
/// produção.
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
    // ── Round-trip byte-a-byte, com o encoder DA VERSÃO ──

    [Fact(DisplayName = "Reidratar a versão 1 e recanonicalizá-la reproduz os bytes congelados, inteiros")]
    public void RoundTrip_VersaoDeAbertura()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        AssertRoundTrip(processo, versao, congelado);
    }

    [Fact(DisplayName = "A versão N>1 tem o bloco retificacao: o round-trip usa a RetificacaoInfo ORIGINAL, recuperada do próprio envelope")]
    public void RoundTrip_VersaoRetificada()
    {
        // O round-trip de uma versão retificada não é o dos blocos da abertura sozinhos. Ela
        // tem também o bloco `retificacao`, que não vem do agregado — é parâmetro externo da
        // canonicalização. Recanonicalizar sem ele produziria um envelope sem esse bloco
        // e a comparação falharia por uma razão que nada tem a ver com a reidratação.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        RetificacaoInfo retificacao = new(CorpusEnvelope.AtoAbertura, "Correção do quadro de vagas do curso de Direito");

        SnapshotCanonico abertura = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo, retificacao));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao v1 = CorpusEnvelope.VersaoDeAbertura(processo, abertura.Bytes);
        VersaoConfiguracao v2 = CorpusEnvelope.VersaoDeRetificacao(v1, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, v2, congelado);

        envelope.Retificacao.Should().NotBeNull("a versão N>1 carrega o bloco retificacao");
        envelope.Retificacao!.EditalRetificadoId.Should().Be(CorpusEnvelope.AtoAbertura);
        envelope.Retificacao.Motivo.Should().Be("Correção do quadro de vagas do curso de Direito");
    }

    [Fact(DisplayName = "O round-trip parte de uma configuração viva DIFERENTE da congelada (é o que o descarte faz)")]
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

    // ── Story #575 — cascataRemanejamento reidrata nos dois estados (presente/ausente) ──

    /// <summary>
    /// O corpus rico (<see cref="CorpusEnvelope.ProcessoRico"/>) tem as 8 modalidades
    /// federais de <c>DistribuicaoLei12711</c> como <c>SegueCascata</c> (INV-12) e, desde a
    /// Story #575, carrega a cascata que as cobre (sem ela <c>Publicar</c> recusaria com
    /// <c>ProcessoSeletivo.CascataOrigemAusente</c>) — é por isso que este teste, como todos
    /// os outros desta classe que publicam o corpus rico, já exercita o estado
    /// <c>presente:true</c> do bloco. Esta asserção é a prova EXPLÍCITA disso, em vez de
    /// deixá-la implícita no round-trip byte-a-byte genérico.
    /// </summary>
    [Fact(DisplayName = "Reidratar um envelope com cascataRemanejamento presente reconstrói ConfiguracaoCascataRemanejamento com a matriz legal completa")]
    public void RoundTrip_ComCascataPresente_ReconstroiConfiguracaoCascataRemanejamento()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, versao, congelado);

        envelope.Grafo.CascataRemanejamento.Should().NotBeNull(
            "o corpus rico tem a cascata configurada (INV-12: as 8 federais de DistribuicaoLei12711 são " +
            "SegueCascata) — a reidratação tem de reconstruí-la, não perdê-la");
        envelope.Grafo.CascataRemanejamento!.FallbackCodigo.Should().Be("AC");
        envelope.Grafo.CascataRemanejamento.Destinos.Should().HaveCount(56,
            "a matriz legal completa tem 8 origens × 7 destinos");
    }

    /// <summary>
    /// O outro estado do bloco: um processo SEM nenhuma modalidade <c>SegueCascata</c> (só
    /// ampla concorrência institucional) não precisa de cascata — <c>PendenciaDaCascata</c>
    /// não a exige — e o envelope congela <c>{"presente":false}</c>. O decoder tem de
    /// reconstruir <see langword="null"/>, não um objeto vazio nem lançar.
    /// </summary>
    [Fact(DisplayName = "Reidratar um envelope com cascataRemanejamento ausente ({\"presente\":false}) reconstrói CascataRemanejamento como null")]
    public void RoundTrip_SemCascata_ReconstroiCascataRemanejamentoComoNull()
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme("PS Sem Cascata");
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            CorpusEnvelope.DadosRicos(), congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            CorpusEnvelope.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(publicacao.Value!);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        reidratado.Value!.Grafo.CascataRemanejamento.Should().BeNull(
            "o processo publicado não tinha nenhuma modalidade SegueCascata — a reidratação não pode inventar uma cascata");
    }

    /// <summary>
    /// O discriminante da issue #1067: <c>obrigatoriedades[]</c> ordena pela CHAVE DE CONTEÚDO
    /// dos objetos completos, não pelo <c>RegraId</c> — mesmo num caso em que ordenar por
    /// <c>RegraId</c> daria o resultado OPOSTO. Um caso com Guids <c>aaaa…</c>/<c>bbbb…</c> não
    /// discriminaria nada aqui: se o Guid crescente coincidir com a ordem de conteúdo, o teste
    /// passa igual com a política antiga (por <c>RegraId</c>) e com a nova (por conteúdo) —
    /// ele não prova qual das duas está de fato em vigor. Por isso <c>regraA</c> recebe
    /// deliberadamente o Guid MAIOR e <c>regraB</c> o MENOR: ordenar por <c>RegraId</c> dá
    /// <c>[REGRA-B, REGRA-A]</c>; ordenar por conteúdo dá <c>[REGRA-A, REGRA-B]</c> — as duas
    /// políticas divergem, e só uma delas sobrevive à asserção final.
    /// </summary>
    [Fact(DisplayName = "O round-trip preserva obrigatoriedades[] ordenada pela chave de conteúdo — mesmo quando ordenar por RegraId daria o resultado oposto")]
    public void RoundTrip_ComConformidadeLegalCongelada_PreservaObrigatoriedadesOrdenadasPorConteudo()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();

        // regraA e regraB só divergem em `atoNormativoUrl`/`portariaInterna`/`baseLegal`/
        // `descricaoHumana`/`vigenciaInicio`/`vigenciaFim`/`hash`/`predicado`/`regraCodigo` — e
        // em `RegraId`, invertido de propósito (ver o <c>summary</c> acima). `aprovada` é IGUAL
        // (true) nos dois: a primeira chave em que os bytes canônicos divergem é
        // `atoNormativoUrl` — string em regraA, null em regraB — e uma string entre aspas
        // (`"` = 0x22) precede o literal `null` (`n` = 0x6E) byte a byte, o que já basta para
        // decidir a ordem de conteúdo a favor de regraA, antes mesmo de chegar a `regraCodigo`.
        RegraAvaliada regraA = new(
            RegraId: new Guid("aaaaaaaa-0000-7000-8000-000000000099"),
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

        RegraAvaliada regraB = new(
            RegraId: new Guid("aaaaaaaa-0000-7000-8000-000000000001"),
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

        // ── Pré-condição 1: a entrada NÃO vem na ordem esperada (conteúdo: A, B). ──
        List<RegraAvaliada> entrada = [regraB, regraA];
        entrada.Select(static r => r.RegraCodigo).Should().NotEqual(["REGRA-A", "REGRA-B"],
            "pré-condição do discriminante: a lista de entrada precisa vir fora da ordem de conteúdo esperada");

        // ── Pré-condição 2: ordenar por RegraId (a política ANTIGA) dá B, A — o OPOSTO do conteúdo. ──
        entrada.OrderBy(static r => r.RegraId).Select(static r => r.RegraCodigo).Should().Equal(
            ["REGRA-B", "REGRA-A"],
            "pré-condição do discriminante: regraB tem o RegraId MENOR — ordenar por RegraId dá B antes de A");

        // ── Pré-condição 3: os bytes canônicos dos objetos COMPLETOS ordenam A, B. ──
        // Reconstrução independente da forma que `obrigatoriedades[]` emite — mesmos 13 campos,
        // mesma tradução de predicado para {tipo, args} que a variante de cada um já usa em
        // produção — para canonicalizar e comparar os bytes SEM chamar o método privado que
        // monta o array (que é o próprio alvo desta prova).
        JsonObject ComoJson(RegraAvaliada regra) => new()
        {
            ["regraId"] = regra.RegraId,
            ["regraCodigo"] = regra.RegraCodigo,
            ["categoria"] = regra.Categoria.ToString(),
            ["tipoProcessoCodigoAvaliado"] = regra.TipoProcessoCodigoAvaliado,
            ["predicado"] = regra.Predicado switch
            {
                ConcorrenciaDuplaObrigatoria => new JsonObject { ["tipo"] = "concorrenciaDuplaObrigatoria", ["args"] = new JsonObject() },
                EtapaObrigatoria p => new JsonObject
                {
                    ["tipo"] = "etapaObrigatoria",
                    ["args"] = new JsonObject { ["tipoEtapaCodigo"] = p.TipoEtapaCodigo },
                },
                _ => throw new NotSupportedException("Oráculo do teste só cobre as duas variantes usadas aqui."),
            },
            ["aprovada"] = regra.Aprovada,
            ["baseLegal"] = regra.BaseLegal,
            ["atoNormativoUrl"] = regra.AtoNormativoUrl,
            ["portariaInterna"] = regra.PortariaInterna,
            ["descricaoHumana"] = regra.DescricaoHumana,
            ["vigenciaInicio"] = regra.VigenciaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["vigenciaFim"] = regra.VigenciaFim?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["hash"] = regra.Hash,
        };

        byte[] bytesA = PerfilCanonicoV1.Instancia.Serializar(ComoJson(regraA));
        byte[] bytesB = PerfilCanonicoV1.Instancia.Serializar(ComoJson(regraB));
        ComparadorLexicograficoDeBytes.Instancia.Compare(bytesA, bytesB).Should().BeLessThan(0,
            "pré-condição do discriminante: os bytes canônicos do objeto completo de regraA precedem os de regraB");

        // ── O discriminante em si: o encoder segue o CONTEÚDO, não o RegraId. ──
        ResultadoConformidade conformidade = new(entrada, []);

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo, conformidade: conformidade));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, versao, congelado);

        JsonArray obrigatoriedadesJson = Envelope(congelado)["documentosExigidos"]!["obrigatoriedades"]!.AsArray();
        obrigatoriedadesJson.Select(o => o!["regraCodigo"]!.GetValue<string>()).Should().Equal(
            ["REGRA-A", "REGRA-B"],
            "o envelope congelado ordena obrigatoriedades[] pela chave de conteúdo — REGRA-A antes de REGRA-B — " +
            "mesmo regraB tendo o RegraId menor");

        envelope.Conformidade.Should().NotBeNull(
            "o restaurador tem de repassar Conformidade adiante na recanonicalização");
        envelope.Conformidade!.Regras.Select(r => r.RegraCodigo).Should().Equal(["REGRA-A", "REGRA-B"],
            "o valor DECODIFICADO também preserva a ordem de conteúdo — não é só o JSON bruto que bate, é a " +
            "sequência que o decoder de fato devolve");
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
                processo, envelope.Dados, envelope.HashDocumento, FusoInstitucional.ZoneId, envelope.Retificacao, envelope.Conformidade,
                envelope.MetadadosFatosCongelados, envelope.ValoresSelecionaveisCongelados));
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

    // ── O etapa.Id é PRESERVADO (asserção direta sobre o decoder) ──

    [Fact(DisplayName = "O decoder preserva o etapa.Id congelado; regenerá-lo faz o etapaRef deixar de resolver")]
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
                EtapaProcesso.Criar(e.Nome, e.Carater, e.TipoEtapa, e.Peso, e.NotaMinima, e.Ordem))],
            ofertaAtendimento: envelope.Grafo.OfertaAtendimento,
            distribuicaoVagas: envelope.Grafo.DistribuicaoVagas,
            bonusRegional: envelope.Grafo.BonusRegional,
            criteriosDesempate: envelope.Grafo.CriteriosDesempate,
            classificacao: envelope.Grafo.Classificacao,
            cronogramaFases: envelope.Grafo.CronogramaFases,
            documentosExigidos: envelope.Grafo.DocumentosExigidos,
            nosExigencia: envelope.Grafo.NosExigencia,
            referenciaTemporalFatos: envelope.Grafo.ReferenciaTemporalFatos,
            fatosColetados: envelope.Grafo.FatosColetados,
            regrasDerivacao: envelope.Grafo.RegrasDerivacao);

        Result recusa = processo.RestaurarConfiguracaoCongelada(versao, comIdRegenerado);

        recusa.IsFailure.Should().BeTrue(
            "regenerar o etapa.Id deixa o etapaRef do desempate e da eliminação apontando para etapas que não " +
            "existem mais — o certame ficaria com desempate e eliminação inexecutáveis");
        recusa.Error!.Code.Should().BeOneOf(
            "ProcessoSeletivo.EtapaRefDesempateInexistente",
            "ProcessoSeletivo.EtapaRefEliminacaoInexistente");
    }

    // ── O decoder LÊ cada campo (matriz fechada de paths) ──

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
    /// nenhum deles muta <c>ordem</c>, <c>id</c>, <c>codigo</c>, <c>ofertaCursoOrigemId</c>,
    /// o interior de <c>regrasEliminacao</c>, nem um elemento de <c>criteriosCumulativos</c>
    /// ou dos demais conjuntos ordenados por chave de conteúdo, issue #1067). Os
    /// campos que <b>são</b> chave de ordenação estão no teste seguinte, com a asserção
    /// primária apenas.
    /// </para>
    /// </remarks>
    [Theory(DisplayName = "O decoder não perde campo: mutar o JSON muda os bytes reidratados")]
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
    /// <remarks>
    /// <c>modalidades.1.criteriosCumulativos.0</c> entrou aqui pela mesma razão de
    /// <c>regrasEliminacao</c> (issue #1067): desde que <c>criteriosCumulativos</c> passou a
    /// ser ordenado pela chave de conteúdo, mutar o valor de UM critério pode mudar a posição
    /// dele entre os dois — a modalidade <c>LB_EP</c> (índice 1 do array) tem exatamente dois
    /// critérios (<see cref="CorpusEnvelope.ProcessoRico"/>), o mínimo para a reordenação ser
    /// observável.
    /// </remarks>
    [Theory(DisplayName = "O decoder lê também os campos que são chave de ordenação")]
    [InlineData("etapas.0.ordem", "9")]
    [InlineData("criteriosDesempate.0.ordem", "9")]
    [InlineData("classificacao.regrasEliminacao.0.args.notaMinima", "88.7500")]
    [InlineData("classificacao.regrasEliminacao.2.args.minimo", "555.0000")]
    [InlineData("modalidades.1.criteriosCumulativos.0", "renda_per_capita_ate_meio_sm")]
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
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        JsonObject mutado = Envelope(congelado);
        Mutar(mutado, caminho, valorNovo);

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
                reidratado.Value.ValoresSelecionaveisCongelados)).Value!.Bytes;

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
                reidratado.Value.HashDocumento, FusoInstitucional.ZoneId,
                reidratado.Value.Retificacao,
                reidratado.Value.Conformidade,
                reidratado.Value.MetadadosFatosCongelados,
                reidratado.Value.ValoresSelecionaveisCongelados)).Value!.Bytes;

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
        "envelope-0.0.12-rico.json"));

    private static string CaminhoNoFonte([CallerFilePath] string origem = "") => Path.Join(
        Path.GetDirectoryName(origem)!,
        "Fixtures",
        "envelope-0.0.12-rico.json");

    // ── Round-trip 1.3 com exigência documental rica (Story #554, PR #903; Story #919, RN08) ──

    /// <summary>
    /// Prova de fidelidade do <c>EnvelopeCodecV13</c> (codec corrente) sobre a MESMA
    /// exigência documental rica que a golden fixture do canonicalizador congela
    /// (<see cref="EnvelopeCanonicoGoldenTests.ProcessoDeReferencia"/>), incluindo o bloco
    /// <c>metadadosFatos</c> (Story #919).
    /// </summary>
    /// <remarks>
    /// Sem arquivo congelado — diferente da golden 1.1 acima, que compara contra bytes
    /// FIXOS gravados no repositório: <see cref="EnvelopeCanonicoGoldenTests.ProcessoDeReferencia"/>
    /// usa <c>Guid.CreateVersion7()</c> para a etapa, a fase e a exigência (a suíte de
    /// origem normaliza esses ids antes de comparar — ver <c>NormalizarIds</c>), então um
    /// arquivo com bytes crus nunca seria estável entre regenerações. A prova aqui é de
    /// AUTOCONSISTÊNCIA (mesmo padrão de <see cref="RoundTrip_VersaoDeAbertura"/>): o
    /// primeiro <c>Codificar</c> É o oráculo — decodificar e recodificar tem de reproduzir
    /// EXATAMENTE os mesmos bytes, quaisquer que sejam os Guids sorteados nesta execução.
    /// </remarks>
    [Fact(DisplayName = "Round-trip 1.3 — reidratar e recanonicalizar o processo de referência (exigência documental rica + metadadosFatos) reproduz os bytes")]
    public void RoundTrip13_ProcessoDeReferenciaComExigenciaDocumentalRica()
    {
        ProcessoSeletivo processo = EnvelopeCanonicoGoldenTests.ProcessoDeReferencia();
        IReadOnlyDictionary<string, MetadadoFatoCongelado> metadadosFatos = EnvelopeCanonicoGoldenTests.MetadadosFatosDeReferencia();
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis =
            EnvelopeCanonicoGoldenTests.ValoresSelecionaveisDeReferencia();
        EntradaCanonicalizacao entrada = new(
            processo, EnvelopeCanonicoGoldenTests.DadosDeReferencia(), EnvelopeCanonicoGoldenTests.HashFixo,
            FusoInstitucional.ZoneId,
            MetadadosFatosCongelados: metadadosFatos,
            ValoresSelecionaveisCongelados: valoresSelecionaveis);
        // Story #923 (bump 1.4): o canonicalizador VIVO passou a emitir 1.4 — esta suíte prova
        // especificamente o EnvelopeCodecV13 (o encoder 1.3 AGORA CONGELADO), não a versão
        // corrente, então a fonte muda para o codec congelado (mesmo padrão que o próprio
        // EnvelopeCodecV13 já documenta para si).
        SnapshotCanonico congelado = new EnvelopeCodec().Codificar(entrada);
        congelado.SchemaVersion.Should().Be("0.0.12", "pré-condição: o codec corrente emite a forma única");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            entrada.Dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            entrada.HashDocumento, "user-sub-123", TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao v1 = publicacao.Value!;

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
                reidratado.Value.ValoresSelecionaveisCongelados)).Value!.Bytes;

        recodificado.Should().Equal(congelado.Bytes,
            "reidratar e recanonicalizar uma exigência documental rica (condicaoGatilho, basesLegais, " +
            "idadeMaximaEmissao, formatoPermitido, tamanhoMaximoBytes, metadadosFatos, valoresSelecionaveis) tem de " +
            "reproduzir os bytes congelados inteiros — qualquer campo perdido pelo decoder sai daqui como divergência");
    }

    // ── Round-trip 1.4 — o bloco novo, arvoreSatisfacao (Story #923) ──

    /// <summary>
    /// Prova de fidelidade do bloco <c>arvoreSatisfacao</c> (Story #923, bump 1.4): uma
    /// árvore com grupo <c>OU</c> + base legal própria, uma folha com cardinalidade
    /// qualificada (<see cref="ChaveDistincao.CompetenciaMensal"/>) e uma folha
    /// <c>repetePorEntidade</c> — as três dimensões que a árvore acumulou nas Stories
    /// #920/#921/#922 e que o snapshot canônico só passa a congelar agora. Sem golden
    /// fixture fixa (mesma razão de <see cref="RoundTrip13_ProcessoDeReferenciaComExigenciaDocumentalRica"/>
    /// — os ids são sorteados); a prova é de AUTOCONSISTÊNCIA, e uma asserção direta sobre
    /// a árvore recomposta fecha o que a comparação de bytes por si só não deixa óbvio.
    /// </summary>
    [Fact(DisplayName = "Round-trip 1.4 — árvore com grupo OU, folha com cardinalidade qualificada e folha repetePorEntidade reproduz os bytes")]
    public void RoundTrip14_ArvoreComGrupoCardinalidadeERepeticao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Árvore 1.4", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: CorpusEnvelope.Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    modalidadeOrigemId: Guid.CreateVersion7(),
                    codigo: "AC",
                    descricao: null,
                    naturezaLegal: NaturezaLegalModalidade.Ampla,
                    composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
                    composicaoOrigemCodigo: null,
                    regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
                    remanejamentoDestino: null,
                    remanejamentoPar: null,
                    remanejamentoFallback: null,
                    criteriosCumulativos: [],
                    acaoQuandoIndeferido: null,
                    baseLegal: "Res. Unifesspa 532/2021",
                    quantidadeDeclarada: 40).Value!,
            ]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: CorpusEnvelope.Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "INSCRICAO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: true,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "INSCRICAO",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // obrigatorio: false — DocumentoExigido.DeterminaResultado() (obrigatória OU
        // consequência declarada) fica falso nas duas folhas, então a checagem de base
        // legal de AvaliarConformidade não exige NoExigenciaBaseLegal delas: só a
        // consequência ELIMINA do GRUPO determina resultado aqui, e a base legal própria
        // dele (baseLegalDoGrupo, abaixo) já cobre isso.
        FormatosPermitidos qualquer = FormatosPermitidos.Criar(true, null).Value!;
        DocumentoExigido rg = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null, qualquer, null).Value!;
        DocumentoExigido comprovanteRenda = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "COMPROVANTE_RENDA", "Comprovante de renda", "SOCIOECONOMICO",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null, qualquer, null).Value!;

        NoExigencia folhaRg = NoExigencia.CriarFolha(
            rg, 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal,
            dataReferencia: new DateOnly(2026, 3, 31)).Value!;
        NoExigencia folhaRenda = NoExigencia.CriarFolha(
            comprovanteRenda, 1, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;
        NoExigenciaBaseLegal baseLegalDoGrupo = NoExigenciaBaseLegal.Criar(
            "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, "Norma interna").Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [baseLegalDoGrupo], [folhaRg, folhaRenda]).Value!;

        processo.DefinirDocumentosExigidos([raiz], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DadosEdital dados = DadosEdital.Criar(
            numero: "099/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: Guid.CreateVersion7()).Value!;
        const string hashDocumento = "2222222222222222222222222222222222222222222222222222222222222222";

        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, hashDocumento, FusoInstitucional.ZoneId));
        congelado.SchemaVersion.Should().Be("0.0.12", "pré-condição: o codec corrente emite a forma única");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "user-sub-arvore", TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao v1 = publicacao.Value!;

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
                reidratado.Value.MetadadosFatosCongelados)).Value!.Bytes;

        recodificado.Should().Equal(congelado.Bytes,
            "reidratar e recanonicalizar uma árvore com grupo OU, cardinalidade qualificada e repetição por " +
            "entidade tem de reproduzir os bytes congelados inteiros — qualquer campo perdido pelo decoder do " +
            "bloco arvoreSatisfacao sai daqui como divergência");

        NoExigencia raizRecarregada = processo.RaizesDeExigencia.Should().ContainSingle().Which;
        raizRecarregada.Tipo.Should().Be(TipoNo.GrupoOu);
        raizRecarregada.Consequencia.Should().Be("ELIMINA");
        raizRecarregada.BasesLegais.Should().ContainSingle().Which.Referencia.Should().Be("Res. Unifesspa 532/2021, art. 12");
        raizRecarregada.Filhos.Should().HaveCount(2);

        NoExigencia folhaRgRecarregada = raizRecarregada.Filhos.Should().ContainSingle(static f => f.ChaveDistincao != null).Which;
        folhaRgRecarregada.QuantidadeMinima.Should().Be(3);
        folhaRgRecarregada.ChaveDistincao.Should().Be(ChaveDistincao.CompetenciaMensal);
        folhaRgRecarregada.DataReferencia.Should().Be(new DateOnly(2026, 3, 31));
        folhaRgRecarregada.DocumentoExigido.Should().NotBeNull();
        folhaRgRecarregada.DocumentoExigido!.TipoDocumentoCodigo.Should().Be("RG");

        NoExigencia folhaRendaRecarregada = raizRecarregada.Filhos.Should().ContainSingle(static f => f.RepetePorEntidade != null).Which;
        folhaRendaRecarregada.RepetePorEntidade.Should().Be(TipoEntidade.MembroNucleoFamiliar);
        folhaRendaRecarregada.DocumentoExigido.Should().NotBeNull();
        folhaRendaRecarregada.DocumentoExigido!.TipoDocumentoCodigo.Should().Be("COMPROVANTE_RENDA");
    }

    // ── issue #1067 — os sete conjuntos ordenados por conteúdo, povoados no mesmo envelope ──

    /// <summary>
    /// Prova que o decoder NÃO reordena (ADR-0109 D9) com os sete arrays da issue #1067
    /// povoados ao mesmo tempo: <c>criteriosCumulativos</c>, <c>ocorrenciasEsperadas</c>,
    /// <c>documentosExigidos.exigencias[].formatosPermitidos.lista</c>,
    /// <c>documentosExigidos.obrigatoriedades</c>, os dois <c>predicado.args</c>
    /// (<c>codigos</c>/<c>necessidades</c>) e <c>documentosExigidos.metadadosFatos[].valoresDominio</c>.
    /// </summary>
    /// <remarks>
    /// A sequência esperada aqui NÃO é um oráculo escrito à mão — essa prova já existe à parte,
    /// por array, no canonicalizador puro. O que só este teste prova é o risco descrito em D2: um
    /// decoder que lesse um destes arrays num dicionário ou conjunto e devolvesse os valores JÁ
    /// reordenados pela MESMA chave que o encoder usa produziria bytes idênticos na
    /// recodificação — passaria pela asserção de bytes sozinha. Por isso a sequência esperada é
    /// extraída do PRÓPRIO JSON congelado e comparada contra o valor <b>imediatamente</b>
    /// decodificado, antes de qualquer recodificação: só um decoder que preserva a ordem lida
    /// (um laço sequencial para <c>List&lt;T&gt;</c>, nunca um dicionário) bate as duas.
    /// </remarks>
    [Fact(DisplayName = "Round-trip com os sete conjuntos da issue #1067 povoados — bytes idênticos e a sequência imediatamente decodificada bate com o JSON")]
    public void RoundTrip_SeteConjuntosDaIssue1067Povoados_PreservaSequenciaImediatamenteDecodificada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Sete Conjuntos", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: ["zzz_criterio", "aaa_criterio"],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: CorpusEnvelope.Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: CorpusEnvelope.Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "INSCRICAO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: true,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "INSCRICAO",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FormatosPermitidos formatosPermitidos = FormatosPermitidos.Criar(
            qualquer: false, entradas: [("PNG", null), ("JPEG", null), ("PDF", null)]).Value!;
        DocumentoExigido documento = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            [], [], null, formatosPermitidos, null).Value!;
        NoExigencia folha = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia,
            ocorrenciasEsperadas: ["ocorrencia_zeta", "ocorrencia_alfa"]).Value!;
        processo.DefinirDocumentosExigidos([folha], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        RegraAvaliada regraModalidadesMinimas = new(
            RegraId: Guid.CreateVersion7(),
            RegraCodigo: "REGRA-MODALIDADES-MINIMAS",
            Categoria: CategoriaObrigatoriedade.Outros,
            TipoProcessoCodigoAvaliado: "SiSU",
            Predicado: new ModalidadesMinimas(["QUI", "AC", "LB_PPI"]),
            Aprovada: true,
            Motivo: null,
            BaseLegal: "Lei de teste",
            AtoNormativoUrl: null,
            PortariaInterna: null,
            DescricaoHumana: "Regra de modalidades mínimas",
            VigenciaInicio: new DateOnly(2020, 1, 1),
            VigenciaFim: null,
            Hash: new string('m', 64));

        RegraAvaliada regraAtendimentoDisponivel = new(
            RegraId: Guid.CreateVersion7(),
            RegraCodigo: "REGRA-ATENDIMENTO-DISPONIVEL",
            Categoria: CategoriaObrigatoriedade.Outros,
            TipoProcessoCodigoAvaliado: "SiSU",
            Predicado: new AtendimentoDisponivel(["VISUAL", "AUDITIVA", "FISICA"]),
            Aprovada: true,
            Motivo: null,
            BaseLegal: "Lei de teste",
            AtoNormativoUrl: null,
            PortariaInterna: null,
            DescricaoHumana: "Regra de atendimento disponível",
            VigenciaInicio: new DateOnly(2020, 1, 1),
            VigenciaFim: null,
            Hash: new string('n', 64));

        ResultadoConformidade conformidade = new([regraModalidadesMinimas, regraAtendimentoDisponivel], []);

        Dictionary<string, MetadadoFatoCongelado> metadadosFatos = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA",
                Dominio: "CATEGORICO",
                Origem: "DECLARADO",
                Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO",
                Binding: "CAMPO_INSCRICAO:COR_RACA",
                ValoresDominio: ["ZETA_VALOR", "ALFA_VALOR"],
                ValoresDominioDeclarados: null),
        };

        DadosEdital dados = DadosEdital.Criar(
            numero: "099/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: Guid.CreateVersion7()).Value!;
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";

        EntradaCanonicalizacao entrada = new(
            processo, dados, hashDocumento, FusoInstitucional.ZoneId, Conformidade: conformidade, MetadadosFatosCongelados: metadadosFatos);
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        congelado.SchemaVersion.Should().Be("0.0.12", "pré-condição: o codec corrente emite a forma única");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "user-sub-sete-conjuntos", TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao v1 = publicacao.Value!;

        JsonObject envelopeJson = Envelope(congelado);
        IReadOnlyList<string> criteriosNoJson =
            [.. envelopeJson["modalidades"]![0]!["criteriosCumulativos"]!.AsArray().Select(static c => c!.GetValue<string>())];
        IReadOnlyList<string> ocorrenciasNoJson =
            [.. envelopeJson["arvoreSatisfacao"]![0]!["ocorrenciasEsperadas"]!.AsArray().Select(static o => o!.GetValue<string>())];
        IReadOnlyList<string> formatosNoJson =
            [.. envelopeJson["documentosExigidos"]!["exigencias"]![0]!["formatosPermitidos"]!["lista"]!.AsArray()
                .Select(static f => f!["formato"]!.GetValue<string>())];
        IReadOnlyList<string> regraCodigosNoJson =
            [.. envelopeJson["documentosExigidos"]!["obrigatoriedades"]!.AsArray().Select(static r => r!["regraCodigo"]!.GetValue<string>())];
        IReadOnlyList<string> valoresDominioNoJson =
            [.. envelopeJson["documentosExigidos"]!["metadadosFatos"]![0]!["valoresDominio"]!.AsArray().Select(static v => v!.GetValue<string>())];
        IReadOnlyList<string> codigosNoJson =
            [.. envelopeJson["documentosExigidos"]!["obrigatoriedades"]!.AsArray()
                .Single(static r => r!["regraCodigo"]!.GetValue<string>() == "REGRA-MODALIDADES-MINIMAS")!
                ["predicado"]!["args"]!["codigos"]!.AsArray().Select(static c => c!.GetValue<string>())];
        IReadOnlyList<string> necessidadesNoJson =
            [.. envelopeJson["documentosExigidos"]!["obrigatoriedades"]!.AsArray()
                .Single(static r => r!["regraCodigo"]!.GetValue<string>() == "REGRA-ATENDIMENTO-DISPONIVEL")!
                ["predicado"]!["args"]!["necessidades"]!.AsArray().Select(static n => n!.GetValue<string>())];

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(v1);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        EnvelopeReidratado envelope = reidratado.Value!;

        // ── A sequência IMEDIATAMENTE decodificada — antes de qualquer recodificação. ──
        envelope.Grafo.DistribuicaoVagas.Single().Modalidades.Single().CriteriosCumulativos.Should().Equal(
            criteriosNoJson, "o decoder tem de preservar a sequência que leu — nunca recalculá-la");
        envelope.Grafo.NosExigencia.Single().OcorrenciasEsperadas.Should().Equal(ocorrenciasNoJson);
        envelope.Grafo.DocumentosExigidos.Single().FormatosPermitidos.Lista!
            .Select(static f => f.Formato.ToCodigo()).Should().Equal(formatosNoJson);
        envelope.Conformidade!.Regras.Select(static r => r.RegraCodigo).Should().Equal(regraCodigosNoJson);
        envelope.MetadadosFatosCongelados!["COR_RACA"].ValoresDominio.Should().Equal(valoresDominioNoJson);

        RegraAvaliada modalidadesMinimasDecodificada = envelope.Conformidade.Regras
            .Single(static r => r.RegraCodigo == "REGRA-MODALIDADES-MINIMAS");
        ((ModalidadesMinimas)modalidadesMinimasDecodificada.Predicado).Codigos.Should().Equal(codigosNoJson);

        RegraAvaliada atendimentoDisponivelDecodificada = envelope.Conformidade.Regras
            .Single(static r => r.RegraCodigo == "REGRA-ATENDIMENTO-DISPONIVEL");
        ((AtendimentoDisponivel)atendimentoDisponivelDecodificada.Predicado).Necessidades.Should().Equal(necessidadesNoJson);

        // ── E os bytes recodificados batem, byte a byte, com os congelados. ──
        processo.RestaurarConfiguracaoCongelada(v1, envelope.Grafo).IsSuccess.Should().BeTrue();

        byte[] recodificado = CorpusEnvelope.Registro.Recodificar(
            v1.SchemaVersion,
            new EntradaCanonicalizacao(
                processo, envelope.Dados, envelope.HashDocumento, FusoInstitucional.ZoneId, envelope.Retificacao, envelope.Conformidade,
                envelope.MetadadosFatosCongelados, envelope.ValoresSelecionaveisCongelados)).Value!.Bytes;

        recodificado.Should().Equal(congelado.Bytes,
            "reidratar e recanonicalizar os sete conjuntos da issue #1067, todos povoados ao mesmo tempo, tem de " +
            "reproduzir os bytes congelados inteiros");
    }

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

    /// <summary>
    /// <c>baseadoEmEnem</c> é o único campo booleano mutado por este helper — sem o ramo
    /// <c>True</c>/<c>False</c>, <c>"true"</c>/<c>"false"</c> virariam string JSON e seriam
    /// recusadas por <see cref="LeitorEnvelope.Booleano"/>, que só aceita
    /// <c>JsonValueKind.True</c>/<c>False</c>.
    /// </summary>
    private static JsonValue ValorComoNo(JsonNode original, string valor) => original.GetValueKind() switch
    {
        System.Text.Json.JsonValueKind.Number => JsonValue.Create(int.Parse(valor, CultureInfo.InvariantCulture)),
        System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False =>
            JsonValue.Create(bool.Parse(valor)),
        _ => JsonValue.Create(valor),
    };

    // ── Round-trip dedicado de baseadoEmEnem — corpus SEM eliminação ENEM (#850) ──

    /// <summary>
    /// Não reaproveita <see cref="MutarEReidratar"/>: o corpus rico (<see cref="CorpusEnvelope.ProcessoRico"/>)
    /// já tem <c>ELIM-CORTE-REDACAO</c>/<c>ELIM-ZERO-EM-AREA</c> (<c>baseadoEmEnem: true</c>) —
    /// mutar o campo para <c>false</c> sobre esse corpus faria o decoder CORRETAMENTE recusar a
    /// reidratação (a invariante ENEM×eliminação é validada dentro de
    /// <see cref="ConfiguracaoClassificacao.Criar"/>), o que quebraria a prova de round-trip em
    /// vez de exercitá-la. Aqui o corpus não tem eliminação ENEM nenhuma, então os dois valores
    /// continuam válidos nos dois sentidos — e cada caso do <see cref="Theory"/> muta para o
    /// valor OPOSTO ao do corpus-base, garantindo bytes diferentes nos dois <see cref="InlineData"/>.
    /// </summary>
    [Theory(DisplayName = "O decoder não perde baseadoEmEnem — corpus sem eliminação ENEM, mutação booleano-consciente")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Decoder_NaoPerdeCampo_BaseadoEmEnem(bool valorBase, bool valorMutado)
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(valorBase);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        JsonObject mutado = Envelope(congelado);
        Mutar(mutado, "classificacao.baseadoEmEnem", valorMutado.ToString(CultureInfo.InvariantCulture));

        byte[] bytesMutados = PerfilCanonicoV1.Instancia.Serializar(mutado);
        bytesMutados.Should().NotEqual(congelado.Bytes, "pré-condição: mutar baseadoEmEnem tem de mudar os bytes");

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
                reidratado.Value.Conformidade)).Value!.Bytes;

        recodificados.Should().NotEqual(congelado.Bytes,
            "o decoder tem de LER 'classificacao.baseadoEmEnem' — se o ignorasse, a recodificação traria o valor " +
            "original de volta");
        recodificados.Should().Equal(bytesMutados,
            "baseadoEmEnem não é chave de ordenação — o resto do envelope reidratado reproduz o mutado inteiro");
    }

    /// <summary>Corpus mínimo sem ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA — os dois valores de <c>baseadoEmEnem</c> continuam válidos.</summary>
    private static ProcessoSeletivo ProcessoSemEliminacaoEnem(bool baseadoEmEnem)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS BaseadoEmEnem", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: CorpusEnvelope.Regra(RegraDistribuicaoVagasCodigo.Institucional, '1'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla,
                    ComposicaoVagasModalidade.ResidualDoVo, null, RegraRemanejamentoModalidade.Nenhuma,
                    null, null, null, [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
            ]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: CorpusEnvelope.Regra(RegraCalculoCodigo.FormulaMediaPonderada, '2'),
            regraArredondamento: CorpusEnvelope.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, '3'),
            casasArredondamento: 2,
            regraOrdemAlocacao: CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, '4'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [],
            baseadoEmEnem: baseadoEmEnem).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([
            FaseCronograma.Criar(
                ordem: 1,
                faseCanonicaOrigemId: Guid.CreateVersion7(),
                codigo: "RESULTADO_FINAL",
                donoInstitucional: "CEPS",
                origemData: OrigemDataFase.Propria,
                agrupaEtapas: true,
                permiteComplementacao: false,
                produzResultado: true,
                resultadoDefinitivo: true,
                coletaInscricao: true,
                inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                atoProduzidoCodigo: "RESULTADO_FINAL",
                atoProduzidoEfeitoIrreversivel: false,
                bancasRequeridas: [],
                regraRecurso: null).Value!,
        ], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }
}
