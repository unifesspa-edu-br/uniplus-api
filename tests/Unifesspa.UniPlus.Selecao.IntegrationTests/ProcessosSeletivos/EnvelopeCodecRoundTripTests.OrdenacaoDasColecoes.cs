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
                envelope.MetadadosFatosCongelados, envelope.ValoresSelecionaveisCongelados,
                envelope.CalendarioDiasUteis, envelope.AgregadosDosGrupos, envelope.ApresentacaoDosPressupostos,
                TiposDeAtoCongelados: envelope.TiposDeAtoCongelados, FatosDerivadosCongelados: envelope.FatosDerivadosCongelados));
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
                EtapaProcesso.Criar(e.Nome, e.Carater, e.TipoEtapa, e.Peso, e.NotaMinima, e.Ordem).Value!)],
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
            regrasDerivacao: envelope.Grafo.RegrasDerivacao,
            numeroDeOpcoesDeCurso: 1);

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
    /// o interior de <c>regrasEliminacao</c>, nem uma condição de <c>regraDeAplicabilidade</c>
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
    [InlineData("etapas.1.tipoEtapa.notaDeOrigemNoEnem", "true")]
    [InlineData("periodo.numero", "099/2026")]
    [InlineData("periodo.inicio", "2026-03-09T03:00:00Z")]
    [InlineData("periodo.fim", "2026-04-30T02:59:59Z")]
    [InlineData("distribuicao.0.regraDistribuicao.versao", "v9")]
    [InlineData("distribuicao.0.referenciaDemografica.censoReferencia", "Censo IBGE 2010")]
    [InlineData("distribuicao.0.referenciaDemografica.baseLegal", "Lei 14.723/2023 art. 3º")]
    [InlineData("modalidades.0.baseLegal", "Outra base legal inteiramente diversa")]
    [InlineData("modalidades.0.descricao", "Outra descrição")]
    [InlineData("modalidades.1.acaoQuandoIndeferido|modalidades.10.acaoQuandoIndeferido", "RECLASSIFICAR_REGRA_EDITAL")]
    [InlineData("bonusRegional.fator", "1.9900")]
    [InlineData("bonusRegional.teto", "42.5000")]
    [InlineData("bonusRegional.identificacao", "Portaria Unifesspa nº 999/2026")]
    [InlineData("bonusRegional.descricao", "Descrição alterada")]
    [InlineData("opcoesDeCurso.numero", "1")]
    [InlineData("classificacao.casasArredondamento", "4")]
    [InlineData("classificacao.regraCalculo.hash", "9999999999999999999999999999999999999999999999999999999999999999")]
    [InlineData("atendimento.condicoes.0.condicaoNome", "Outra condição")]
    [InlineData("atendimento.recursos.0.recursoCodigo", "INTERPRETE_LIBRAS")]
    [InlineData("atendimento.recursos.0.recursoNome", "Intérprete de Libras")]
    [InlineData("atendimento.tiposDeficiencia.0.tipoDeficienciaNome", "Deficiência física")]
    [InlineData("identificadorLegivel", "outro-certame-2026")]
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
}
