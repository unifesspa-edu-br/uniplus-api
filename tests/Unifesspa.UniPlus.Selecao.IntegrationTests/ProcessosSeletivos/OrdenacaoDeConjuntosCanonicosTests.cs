namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Issue #1067 — os sete arrays do envelope que são <b>conjuntos</b> (sem precedência entre os
/// elementos) passam a ordenar pela <b>chave de conteúdo</b> (ADR-0109 D9): os bytes canônicos
/// do próprio item, não a ordem de entrada nem a identidade técnica da linha.
/// </summary>
/// <remarks>
/// <para>
/// Cada teste de <c>CA-01</c> tem oráculo <b>independente</b> — a ordem esperada é escrita à
/// mão a partir do primeiro byte UTF-8 em que os itens divergem, nunca calculada chamando o
/// método privado que ordena (<c>EscritaCanonica.OrdenarPorConteudo</c>). A
/// mesma asserção também serve de <c>CA-02</c> quando o teste reordena a entrada e exige os
/// MESMOS bytes na saída — determinismo canônico, não ausência de semântica (essa vem da
/// evidência de consumidor documentada no plano da issue, não de um teste de permutação).
/// </para>
/// <para>
/// A projeção é pura (ADR-0109 D6) — nenhum teste aqui precisa de banco.
/// </para>
/// </remarks>
public sealed class OrdenacaoDeConjuntosCanonicosTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizador = new();

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo,
        ResultadoConformidade? conformidade = null,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatos = null) =>
        new(processo, Dados(), new string('0', 64), FusoInstitucional.ZoneId, Conformidade: conformidade, Retificacao: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(MetadadosFatosCongelados: metadadosFatos));

    /// <summary>
    /// Serializa um FRAGMENTO do envelope (não o envelope inteiro) pelas mesmas regras de bytes
    /// do perfil — usado para comparar dois fragmentos byte a byte sem depender dos Guids
    /// aleatórios do resto do envelope em que cada um está embutido (<see cref="MontarProcesso"/>
    /// não fixa ids: dois processos distintos nunca têm o envelope INTEIRO byte-idêntico, mas o
    /// fragmento sob teste tem de ser). <c>internal</c>: reaproveitado por
    /// <c>PredicadoDnfOrdenacaoCanonicaTests</c> (issue #1068), mesma necessidade de comparar
    /// fragmento em vez de envelope inteiro.
    /// </summary>
    internal static byte[] BytesDoFragmento(JsonNode fragmento) =>
        PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["x"] = fragmento.DeepClone() });

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "INSCRICAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: true, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>
    /// Um processo mínimo, válido para <see cref="SnapshotPublicacaoCanonicalizer.Canonicalizar"/>,
    /// com os dois knobs que os testes de <c>ocorrenciasEsperadas</c> e
    /// <c>formatosPermitidos.lista</c> precisam variar — os outros quatro arrays da issue
    /// (<c>obrigatoriedades</c>, os dois <c>predicado.parametros</c> e <c>valoresDominio</c>) entram
    /// pela <see cref="EntradaCanonicalizacao"/>, não pelo agregado, e não precisam deste builder.
    /// </summary>
    private static ProcessoSeletivo MontarProcesso(
        IReadOnlyList<string>? ocorrenciasEsperadas = null,
        IReadOnlyList<(string Formato, int? TamanhoMaximoBytesMax)>? formatosPermitidos = null)
    {
        ProcessoSeletivo processo = CriarProcesso();
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        DefinirDistribuicao(processo);

        DefinirClassificacao(processo);
        FaseCronograma fase = DefinirCronograma(processo);
        DefinirDocumento(processo, fase, ocorrenciasEsperadas, formatosPermitidos);

        return processo;
    }

    private static ProcessoSeletivo CriarProcesso() =>
        ProcessoSeletivo.Criar(
            "PS Ordenação de Conjuntos", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
        }).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();
    }

    private static void DefinirDistribuicao(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = Modalidade();
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static ModalidadeSelecionada Modalidade() => ModalidadeSelecionada.Criar(
        modalidadeOrigemId: Guid.CreateVersion7(),
        codigo: "AC",
        descricao: null,
        naturezaLegal: NaturezaLegalModalidade.Ampla,
        composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
        composicaoOrigemCodigo: null,
        regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
        remanejamentoDestino: null,
        remanejamentoPar: null,
        remanejamentoFallback: null,
        regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
        acaoQuandoIndeferido: null,
        baseLegal: "Res. Unifesspa 532/2021",
        quantidadeDeclarada: 40).Value!;

    private static FaseCronograma DefinirCronograma(ProcessoSeletivo processo)
    {
        FaseCronograma fase = FaseDeInscricao();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "INSCRICAO"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();

        return fase;
    }

    private static void DefinirDocumento(ProcessoSeletivo processo, FaseCronograma fase,
        IReadOnlyList<string>? ocorrenciasEsperadas, IReadOnlyList<(string Formato, int? TamanhoMaximoBytesMax)>? formatosPermitidos)
    {
        if (ocorrenciasEsperadas is not null || formatosPermitidos is not null)
        {
            FormatosPermitidos formatosAceitos = formatosPermitidos is not null
                ? FormatosPermitidos.Criar(qualquer: false, entradas: [.. formatosPermitidos]).Value!
                : FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!;

            RegraDeComprovacao regraDeDocumento = RegraDeDocumento(fase, formatosAceitos, ocorrenciasEsperadas);

            processo.DefinirDocumentosExigidos([regraDeDocumento]).IsSuccess.Should().BeTrue();
        }
    }

    private static RegraDeComprovacao RegraDeDocumento(FaseCronograma fase, FormatosPermitidos formatosAceitos, IReadOnlyList<string>? ocorrenciasEsperadas)
    {
        DocumentoExigido documento = DocumentoExigido.Criar(
            new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
            new MomentoDaExigencia(fase.Id, null, FinalidadeFormulario.Inscricao),
            new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
            new IndeferimentoDoDocumento(false, null, []),
            RequisitosDoDocumentoDeTeste.Com(formatosAceitos),
            null).Value!;

        return ocorrenciasEsperadas is not null
            ? RegraDeComprovacao.CriarRegraDeDocumento(
                documento, 0, quantidadeMinima: ocorrenciasEsperadas.Count,
                criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia, ocorrenciasEsperadas: ocorrenciasEsperadas).Value!
            : RegraDeComprovacao.CriarRegraDeDocumento(documento, 0).Value!;
    }

    // ── ocorrenciasEsperadas ──

    [Fact(DisplayName = "ocorrenciasEsperadas ordena pela chave de conteúdo — oráculo escrito à mão")]
    public void OcorrenciasEsperadas_OrdenaPelaChaveDeConteudo()
    {
        JsonArray ocorrenciasUm = Ocorrencias(["MARCO", "JANEIRO", "FEVEREIRO"]);

        ocorrenciasUm.Select(static o => o!.GetValue<string>()).Should().Equal(
            ["FEVEREIRO", "JANEIRO", "MARCO"],
            "'F' (0x46) < 'J' (0x4A) < 'M' (0x4D) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        JsonArray ocorrenciasDois = Ocorrencias(["FEVEREIRO", "JANEIRO", "MARCO"]);

        BytesDoFragmento(ocorrenciasDois).Should().Equal(BytesDoFragmento(ocorrenciasUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }

    // ── documentosExigidos.exigencias[].formatosPermitidos.lista ──

    [Fact(DisplayName = "formatosPermitidos.lista ordena pela chave de conteúdo do objeto completo — oráculo escrito à mão")]
    public void FormatosPermitidosLista_OrdenaPelaChaveDeConteudo()
    {
        JsonArray listaUm = Formatos([("PNG", null), ("PDF", null), ("JPEG", null)]);

        listaUm.Select(static f => f!["formato"]!.GetValue<string>()).Should().Equal(
            ["JPEG", "PDF", "PNG"],
            "os três objetos só divergem na chave 'formato' (chave alfabeticamente ANTES de " +
            "'tamanhoMaximoBytesMax') — 'J' (0x4A) < 'P'+'D' (0x50 0x44) < 'P'+'N' (0x50 0x4E)");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        JsonArray listaDois = Formatos([("JPEG", null), ("PDF", null), ("PNG", null)]);

        BytesDoFragmento(listaDois).Should().Equal(BytesDoFragmento(listaUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }

    // ── documentosExigidos.obrigatoriedades ──

    private static RegraAvaliada RegraDeConformidade(string codigo, Guid id) => new(
        RegraId: id,
        RegraCodigo: codigo,
        Categoria: CategoriaObrigatoriedade.Outros,
        TipoProcessoCodigoAvaliado: "SiSU",
        Predicado: new ConcorrenciaDuplaObrigatoria(),
        Aprovada: true,
        Motivo: null,
        BaseLegal: "Lei de teste",
        AtoNormativoUrl: null,
        PortariaInterna: null,
        DescricaoHumana: "Regra de teste",
        VigenciaInicio: new DateOnly(2020, 1, 1),
        VigenciaFim: null,
        Hash: new string('x', 64));

    /// <summary>
    /// Três regras que só divergem em <c>regraCodigo</c> (e em <c>RegraId</c>, irrelevante para
    /// o conteúdo): a chave de conteúdo se reduz à ordem alfabética de <c>regraCodigo</c>. Os
    /// <c>RegraId</c> são escolhidos em ordem INVERSA à alfabética de propósito — ordenar por
    /// <c>RegraId</c> (a política antiga) daria <c>[ZETA, MEIO, ALFA]</c>, o oposto do oráculo.
    /// </summary>
    [Fact(DisplayName = "obrigatoriedades ordena pela chave de conteúdo do objeto completo — oráculo escrito à mão, RegraId em ordem inversa")]
    public void Obrigatoriedades_OrdenaPelaChaveDeConteudo()
    {
        RegraAvaliada alfa = RegraDeConformidade("ALFA", new Guid("bbbbbbbb-0000-7000-8000-000000000099"));
        RegraAvaliada meio = RegraDeConformidade("MEIO", new Guid("bbbbbbbb-0000-7000-8000-000000000050"));
        RegraAvaliada zeta = RegraDeConformidade("ZETA", new Guid("bbbbbbbb-0000-7000-8000-000000000001"));

        List<RegraAvaliada> entradaUm = [zeta, alfa, meio];
        ConferirOrdemPorIdentificador(zeta, alfa, meio);

        JsonArray obrigatoriedadesUm = EnvelopeDeRegras(entradaUm)["documentosExigidos"]!["obrigatoriedades"]!.AsArray();

        ConferirOrdemDasObrigatoriedades(obrigatoriedadesUm);

        ConferirDeterminismoDasObrigatoriedades(alfa, meio, zeta, obrigatoriedadesUm);
    }

    // ── documentosExigidos.obrigatoriedades[].predicado.parametros.codigos (ModalidadesMinimas) ──

    [Fact(DisplayName = "predicado.parametros.codigos (ModalidadesMinimas) ordena pela chave de conteúdo — oráculo escrito à mão")]
    public void PredicadoModalidadesMinimas_Codigos_OrdenaPelaChaveDeConteudo()
    {
        RegraAvaliada regraUm = RegraModalidadesMinimas(["QUI", "AC", "LB_PPI"]);

        JsonArray codigosUm = EnvelopeDeRegras([regraUm])["documentosExigidos"]!["obrigatoriedades"]![0]!["condicoes"]!["parametros"]!["codigos"]!.AsArray();

        codigosUm.Select(static c => c!.GetValue<string>()).Should().Equal(
            ["AC", "LB_PPI", "QUI"],
            "'A' (0x41) < 'L' (0x4C) < 'Q' (0x51) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        RegraAvaliada regraDois = RegraModalidadesMinimas(["AC", "LB_PPI", "QUI"]);
        JsonArray codigosDois = EnvelopeDeRegras([regraDois])["documentosExigidos"]!["obrigatoriedades"]![0]!["condicoes"]!["parametros"]!["codigos"]!.AsArray();

        BytesDoFragmento(codigosDois).Should().Equal(BytesDoFragmento(codigosUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }

    // ── documentosExigidos.obrigatoriedades[].predicado.parametros.necessidades (AtendimentoDisponivel) ──

    [Fact(DisplayName = "predicado.parametros.necessidades (AtendimentoDisponivel) ordena pela chave de conteúdo — oráculo escrito à mão")]
    public void PredicadoAtendimentoDisponivel_Necessidades_OrdenaPelaChaveDeConteudo()
    {
        RegraAvaliada regraUm = RegraAtendimentoDisponivel(["VISUAL", "AUDITIVA", "FISICA"]);

        JsonArray necessidadesUm = EnvelopeDeRegras([regraUm])["documentosExigidos"]!["obrigatoriedades"]![0]!["condicoes"]!["parametros"]!["necessidades"]!.AsArray();

        necessidadesUm.Select(static n => n!.GetValue<string>()).Should().Equal(
            ["AUDITIVA", "FISICA", "VISUAL"],
            "'A' (0x41) < 'F' (0x46) < 'V' (0x56) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        RegraAvaliada regraDois = RegraAtendimentoDisponivel(["FISICA", "AUDITIVA", "VISUAL"]);
        JsonArray necessidadesDois = EnvelopeDeRegras([regraDois])["documentosExigidos"]!["obrigatoriedades"]![0]!["condicoes"]!["parametros"]!["necessidades"]!.AsArray();

        BytesDoFragmento(necessidadesDois).Should().Equal(BytesDoFragmento(necessidadesUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }

    // ── documentosExigidos.metadadosFatos[].valoresDominio ──

    [Fact(DisplayName = "definicoesDosDados[].valoresDominio ordena pela chave de conteúdo — oráculo escrito à mão")]
    public void MetadadosFatos_ValoresDominio_OrdenaPelaChaveDeConteudo()
    {
        JsonArray valoresUm = ValoresDeDominio(["PRETA", "AMARELA", "BRANCA"]);

        valoresUm.Select(static v => v!.GetValue<string>()).Should().Equal(
            ["AMARELA", "BRANCA", "PRETA"],
            "'A' (0x41) < 'B' (0x42) < 'P' (0x50) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        JsonArray valoresDois = ValoresDeDominio(["BRANCA", "PRETA", "AMARELA"]);

        BytesDoFragmento(valoresDois).Should().Equal(BytesDoFragmento(valoresUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }

    private static void ConferirDeterminismoDasObrigatoriedades(
        RegraAvaliada alfa, RegraAvaliada meio, RegraAvaliada zeta, JsonArray obrigatoriedadesUm)
    {
        // CA-02 (determinismo) — MESMO conteúdo, ordem de entrada DIFERENTE, MESMOS bytes. Nota:
        // esta parte específica já valia ANTES da correção — ordenar por RegraId também é
        // invariante à ordem de entrada, só usa uma chave diferente (e errada). A permutação
        // sozinha prova determinismo, não qual das duas políticas está em vigor; quem discrimina
        // isso é o teste acima (RegraId em ordem inversa ao conteúdo) e o CA-03 do round-trip.
        List<RegraAvaliada> entradaDois = [meio, zeta, alfa];
        JsonArray obrigatoriedadesDois = EnvelopeDeRegras(entradaDois)["documentosExigidos"]!["obrigatoriedades"]!.AsArray();

        BytesDoFragmento(obrigatoriedadesDois).Should().Equal(BytesDoFragmento(obrigatoriedadesUm),
            "duas entradas com o MESMO conjunto de regras em ordens DIFERENTES produzem o mesmo array canônico");
    }

    private static void ConferirOrdemDasObrigatoriedades(JsonArray obrigatoriedadesUm)
    {
        obrigatoriedadesUm.Select(static o => o!["regraCodigo"]!.GetValue<string>()).Should().Equal(
            ["ALFA", "MEIO", "ZETA"],
            "as três regras só divergem em regraCodigo — a chave de conteúdo se reduz à ordem alfabética dele, " +
            "não ao RegraId");
    }

    private static void ConferirOrdemPorIdentificador(RegraAvaliada zeta, RegraAvaliada alfa, RegraAvaliada meio)
    {
        new[] { zeta, alfa, meio }.OrderBy(static r => r.RegraId).Select(static r => r.RegraCodigo).Should().Equal(
            ["ZETA", "MEIO", "ALFA"],
            "pré-condição: ordenar por RegraId (a política ANTIGA) dá o oposto do oráculo de conteúdo abaixo");
    }

    private static RegraAvaliada RegraModalidadesMinimas(IReadOnlyList<string> valores) =>
        RegraDeConformidade("REGRA-MM", Guid.CreateVersion7()) with { Predicado = new ModalidadesMinimas(valores) };

    private static RegraAvaliada RegraAtendimentoDisponivel(IReadOnlyList<string> valores) =>
        RegraDeConformidade("REGRA-AD", Guid.CreateVersion7()) with { Predicado = new AtendimentoDisponivel(valores) };

    private static JsonArray Ocorrencias(IReadOnlyList<string> ocorrencias) =>
        ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(MontarProcesso(ocorrenciasEsperadas: ocorrencias))))
            ["regrasDeComprovacao"]![0]!["ocorrenciasEsperadas"]!.AsArray();

    private static JsonArray Formatos(IReadOnlyList<(string Formato, int? TamanhoMaximoBytesMax)> formatos) =>
        ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(MontarProcesso(formatosPermitidos: formatos))))
            ["documentosExigidos"]!["exigencias"]![0]!["formatosPermitidos"]!["lista"]!.AsArray();

    private static JsonObject EnvelopeDeRegras(IReadOnlyList<RegraAvaliada> regras) =>
        ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(
            Entrada(MontarProcesso(), conformidade: new ResultadoConformidade(regras, []))));

    private static JsonArray ValoresDeDominio(IReadOnlyList<string> valores)
    {
        Dictionary<string, MetadadoFatoCongelado> metadados = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA", Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO", Binding: "CAMPO_FORMULARIO:COR_RACA",
                ValoresDominio: valores, ValoresDominioDeclarados: null),
        };
        return ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(MontarProcesso(), metadadosFatos: metadados)))
            ["documentosExigidos"]!["definicoesDosDados"]![0]!["opcoes"]!.AsArray();
    }
}
