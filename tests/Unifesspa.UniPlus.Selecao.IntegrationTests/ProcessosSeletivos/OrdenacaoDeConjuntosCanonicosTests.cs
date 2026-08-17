namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

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
/// método privado que ordena (<c>SnapshotPublicacaoCanonicalizer.OrdenarPorConteudo</c>). A
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
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo,
        ResultadoConformidade? conformidade = null,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatos = null) =>
        new(processo, Dados(), new string('0', 64), FusoInstitucional.ZoneId, Conformidade: conformidade, MetadadosFatosCongelados: metadadosFatos);

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

    /// <summary>
    /// Um processo mínimo, válido para <see cref="SnapshotPublicacaoCanonicalizer.Canonicalizar"/>,
    /// com os três knobs que os testes de <c>criteriosCumulativos</c>, <c>ocorrenciasEsperadas</c>
    /// e <c>formatosPermitidos.lista</c> precisam variar — os outros quatro arrays da issue
    /// (<c>obrigatoriedades</c>, os dois <c>predicado.args</c> e <c>valoresDominio</c>) entram
    /// pela <see cref="EntradaCanonicalizacao"/>, não pelo agregado, e não precisam deste builder.
    /// </summary>
    private static ProcessoSeletivo MontarProcesso(
        IReadOnlyList<string>? criteriosCumulativos = null,
        IReadOnlyList<string>? ocorrenciasEsperadas = null,
        IReadOnlyList<(string Formato, int? TamanhoMaximoBytesMax)>? formatosPermitidos = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Ordenação de Conjuntos", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!,
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
            criteriosCumulativos: criteriosCumulativos ?? [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [],
            baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

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

        if (ocorrenciasEsperadas is not null || formatosPermitidos is not null)
        {
            FormatosPermitidos fp = formatosPermitidos is not null
                ? FormatosPermitidos.Criar(qualquer: false, entradas: [.. formatosPermitidos]).Value!
                : FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!;

            DocumentoExigido documento = DocumentoExigido.Criar(
                fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL",
                Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
                [], [], null, fp, null).Value!;

            NoExigencia folha = ocorrenciasEsperadas is not null
                ? NoExigencia.CriarFolha(
                    documento, 0, quantidadeMinima: ocorrenciasEsperadas.Count,
                    chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: ocorrenciasEsperadas).Value!
                : NoExigencia.CriarFolha(documento, 0).Value!;

            processo.DefinirDocumentosExigidos([folha], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    // ── criteriosCumulativos ──

    /// <summary>
    /// Oráculo por BYTE UTF-8 (não por unidade de código UTF-16). <c>System.Text.Json</c> — com
    /// QUALQUER <c>JavaScriptEncoder</c>, inclusive <c>UnsafeRelaxedJsonEscaping</c> — escapa todo
    /// caractere astral (acima de <c>U+FFFF</c>) como par substituto textual (<c>\uD83D\uDE00</c>
    /// para 😀), porque o mecanismo de intervalos permitidos do encoder só cobre o plano básico
    /// multilíngue: não existe como "permitir" um caractere astral passar literal. Por isso o
    /// item escapado começa com <c>\</c> (<c>0x5C</c>) — byte MENOR que o de qualquer caractere
    /// literal não-ASCII (que começa em <c>0xC2</c> ou mais). Um caractere comum como <c>'á'</c>
    /// (literal, UTF-8 <c>0xC3 0xA1</c>) tem codepoint (<c>U+00E1</c> = 225) bem MENOR que a
    /// primeira unidade de código do par substituto de 😀 (<c>0xD83D</c> = 55357) — a ordem
    /// ORDINAL (<see cref="string.CompareOrdinal(string, string)"/>) põe <c>'á'</c> antes de 😀. A
    /// ordem por BYTE (<see cref="ComparadorLexicograficoDeBytes"/>, a que o perfil usa) faz o
    /// oposto: 😀 (escapado, primeiro byte <c>0x5C</c>) precede <c>'á'</c> (literal, primeiro byte
    /// <c>0xC3</c>). Sem um caso acima de <c>U+FFFF</c> nesta posição, o teste provaria só
    /// ordenação ASCII e não distinguiria as duas políticas de comparação.
    /// </summary>
    [Fact(DisplayName = "criteriosCumulativos ordena pela chave de conteúdo em bytes UTF-8 — oráculo escrito à mão, com caractere acima de U+FFFF")]
    public void CriteriosCumulativos_OrdenaPelaChaveDeConteudoEmBytesUtf8()
    {
        string.CompareOrdinal("área", "😀").Should().BeLessThan(0,
            "pré-condição: por ordem ORDINAL (UTF-16), 'área' (U+00E1 = 225) precede 😀 (par substituto " +
            "iniciado em 0xD83D = 55357) — o oposto do oráculo de bytes abaixo");

        ProcessoSeletivo entradaUm = MontarProcesso(criteriosCumulativos: ["😀", "banana", "área", "Apple"]);
        JsonArray criteriosUm = EnvelopeCodecRoundTripTests.Envelope(
            Canonicalizador.Canonicalizar(Entrada(entradaUm)))["modalidades"]![0]!["criteriosCumulativos"]!.AsArray();

        criteriosUm.Select(static c => c!.GetValue<string>()).Should().Equal(
            ["Apple", "😀", "banana", "área"],
            "ordem por PRIMEIRO BYTE: 'A' (0x41) < 😀 escapado (0x5C, o '\\' do \\uD83D\\uDE00) < 'b' (0x62) < " +
            "'á' literal (0xC3) — 😀 vem ANTES de 'banana' e 'área' em bytes, mesmo tendo o maior codepoint");

        // CA-02 — a MESMA entrada em outra ordem produz os MESMOS bytes (determinismo canônico).
        ProcessoSeletivo entradaDois = MontarProcesso(criteriosCumulativos: ["Apple", "área", "banana", "😀"]);
        JsonArray criteriosDois = EnvelopeCodecRoundTripTests.Envelope(
            Canonicalizador.Canonicalizar(Entrada(entradaDois)))["modalidades"]![0]!["criteriosCumulativos"]!.AsArray();

        BytesDoFragmento(criteriosDois).Should().Equal(BytesDoFragmento(criteriosUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico — a posição " +
            "de entrada não sobrevive à canonicalização");
    }

    /// <summary>
    /// Contraprova de forma composta e decomposta (D1) — e não só "os mesmos bytes no fim", mas
    /// uma ORDEM que só bate se a normalização acontecer ANTES da comparação, não depois. Sem
    /// normalizar, os bytes CRUS de <c>decomposta</c> (<c>63-61-66-65-CC-81</c>, terminando em
    /// <c>'e'</c> 0x65 + acento combinante) comparam MENOR que <c>meio</c> (<c>63-61-66-7A…</c>,
    /// quarto byte <c>'z'</c> 0x7A) — <c>decomposta</c> viria ANTES. Depois de normalizar (NFC),
    /// os mesmos bytes de <c>composta</c>/<c>decomposta</c> começam com o 'é' pré-composto
    /// (<c>C3-A9</c> no quarto byte, dentro das aspas do JSON) — MAIOR que <c>'z'</c> — e
    /// <c>café</c> vem DEPOIS de <c>meio</c>. Se a chave de ordenação serializasse o texto FORA
    /// do perfil (sem passar por <see cref="PerfilCanonicoV1.SerializarChave"/>), ela ordenaria
    /// pela forma crua e produziria <c>[decomposta, meio]</c> — o oposto do oráculo abaixo.
    /// </summary>
    [Fact(DisplayName = "criteriosCumulativos: a chave de ordenação usa a forma NFC — 'café' decomposto ordena como se já fosse composto")]
    public void CriteriosCumulativos_ChaveDeOrdenacaoUsaFormaNfc_NaoOTextoCru()
    {
        const string composta = "café"; // "café" — já é a forma NFC.
        string decomposta = "café"; // "cafe" + acento agudo combinante (U+0301) — é NFD.
        const string meio = "cafz_ponto_medio"; // Entre as duas ordens possíveis de 'café' — ver o summary.

        composta.Should().NotBe(decomposta,
            "pré-condição: são sequências de code points DIFERENTES para o mesmo texto visível");
        composta.Should().Be(composta.Normalize(NormalizationForm.FormC),
            "pré-condição: 'café' (com 'é' pré-composto) já é a forma NFC");
        Encoding.UTF8.GetBytes(decomposta).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(meio)).Should().BeLessThan(0,
            "pré-condição: SEM normalizar, os bytes crus de 'café' decomposto comparam ANTES de 'cafz_ponto_medio' " +
            "— o oposto do oráculo de conteúdo abaixo, que normaliza antes de comparar");

        JsonArray criteriosComposta = EnvelopeCodecRoundTripTests.Envelope(
            Canonicalizador.Canonicalizar(Entrada(MontarProcesso(criteriosCumulativos: [composta, meio]))))
            ["modalidades"]![0]!["criteriosCumulativos"]!.AsArray();

        criteriosComposta.Select(static c => c!.GetValue<string>()).Should().Equal(
            [meio, composta],
            "'cafz_ponto_medio' (quarto byte 'z' = 0x7A) precede 'café' (quarto byte, dentro das aspas, é o " +
            "primeiro byte do 'é' pré-composto = 0xC3) — só verdade DEPOIS de normalizar; o texto cru ordenaria " +
            "ao contrário (ver pré-condição acima)");

        JsonArray criteriosDecomposta = EnvelopeCodecRoundTripTests.Envelope(
            Canonicalizador.Canonicalizar(Entrada(MontarProcesso(criteriosCumulativos: [decomposta, meio]))))
            ["modalidades"]![0]!["criteriosCumulativos"]!.AsArray();

        BytesDoFragmento(criteriosDecomposta).Should().Equal(BytesDoFragmento(criteriosComposta),
            "o item decomposto ordena e serializa EXATAMENTE como o composto equivalente — mesma posição, mesmos " +
            "bytes — porque a chave de ordenação passa pela mesma normalização NFC que a emissão final");
    }

    // ── ocorrenciasEsperadas ──

    [Fact(DisplayName = "ocorrenciasEsperadas ordena pela chave de conteúdo — oráculo escrito à mão")]
    public void OcorrenciasEsperadas_OrdenaPelaChaveDeConteudo()
    {
        ProcessoSeletivo entradaUm = MontarProcesso(ocorrenciasEsperadas: ["MARCO", "JANEIRO", "FEVEREIRO"]);
        JsonArray ocorrenciasUm = EnvelopeCodecRoundTripTests.Envelope(
            Canonicalizador.Canonicalizar(Entrada(entradaUm)))["arvoreSatisfacao"]![0]!["ocorrenciasEsperadas"]!.AsArray();

        ocorrenciasUm.Select(static o => o!.GetValue<string>()).Should().Equal(
            ["FEVEREIRO", "JANEIRO", "MARCO"],
            "'F' (0x46) < 'J' (0x4A) < 'M' (0x4D) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        ProcessoSeletivo entradaDois = MontarProcesso(ocorrenciasEsperadas: ["FEVEREIRO", "JANEIRO", "MARCO"]);
        JsonArray ocorrenciasDois = EnvelopeCodecRoundTripTests.Envelope(
            Canonicalizador.Canonicalizar(Entrada(entradaDois)))["arvoreSatisfacao"]![0]!["ocorrenciasEsperadas"]!.AsArray();

        BytesDoFragmento(ocorrenciasDois).Should().Equal(BytesDoFragmento(ocorrenciasUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }

    // ── documentosExigidos.exigencias[].formatosPermitidos.lista ──

    [Fact(DisplayName = "formatosPermitidos.lista ordena pela chave de conteúdo do objeto completo — oráculo escrito à mão")]
    public void FormatosPermitidosLista_OrdenaPelaChaveDeConteudo()
    {
        ProcessoSeletivo entradaUm = MontarProcesso(
            formatosPermitidos: [("PNG", null), ("PDF", null), ("JPEG", null)]);
        JsonArray listaUm = EnvelopeCodecRoundTripTests.Envelope(
            Canonicalizador.Canonicalizar(Entrada(entradaUm)))
            ["documentosExigidos"]!["exigencias"]![0]!["formatosPermitidos"]!["lista"]!.AsArray();

        listaUm.Select(static f => f!["formato"]!.GetValue<string>()).Should().Equal(
            ["JPEG", "PDF", "PNG"],
            "os três objetos só divergem na chave 'formato' (chave alfabeticamente ANTES de " +
            "'tamanhoMaximoBytesMax') — 'J' (0x4A) < 'P'+'D' (0x50 0x44) < 'P'+'N' (0x50 0x4E)");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        ProcessoSeletivo entradaDois = MontarProcesso(
            formatosPermitidos: [("JPEG", null), ("PDF", null), ("PNG", null)]);
        JsonArray listaDois = EnvelopeCodecRoundTripTests.Envelope(
            Canonicalizador.Canonicalizar(Entrada(entradaDois)))
            ["documentosExigidos"]!["exigencias"]![0]!["formatosPermitidos"]!["lista"]!.AsArray();

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
        new[] { zeta, alfa, meio }.OrderBy(static r => r.RegraId).Select(static r => r.RegraCodigo).Should().Equal(
            ["ZETA", "MEIO", "ALFA"],
            "pré-condição: ordenar por RegraId (a política ANTIGA) dá o oposto do oráculo de conteúdo abaixo");

        ProcessoSeletivo processoUm = MontarProcesso();
        SnapshotCanonico snapshotUm = Canonicalizador.Canonicalizar(
            Entrada(processoUm, conformidade: new ResultadoConformidade(entradaUm, [])));
        JsonArray obrigatoriedadesUm = EnvelopeCodecRoundTripTests.Envelope(snapshotUm)
            ["documentosExigidos"]!["obrigatoriedades"]!.AsArray();

        obrigatoriedadesUm.Select(static o => o!["regraCodigo"]!.GetValue<string>()).Should().Equal(
            ["ALFA", "MEIO", "ZETA"],
            "as três regras só divergem em regraCodigo — a chave de conteúdo se reduz à ordem alfabética dele, " +
            "não ao RegraId");

        // CA-02 (determinismo) — MESMO conteúdo, ordem de entrada DIFERENTE, MESMOS bytes. Nota:
        // esta parte específica já valia ANTES da correção — ordenar por RegraId também é
        // invariante à ordem de entrada, só usa uma chave diferente (e errada). A permutação
        // sozinha prova determinismo, não qual das duas políticas está em vigor; quem discrimina
        // isso é o teste acima (RegraId em ordem inversa ao conteúdo) e o CA-03 do round-trip.
        List<RegraAvaliada> entradaDois = [meio, zeta, alfa];
        ProcessoSeletivo processoDois = MontarProcesso();
        SnapshotCanonico snapshotDois = Canonicalizador.Canonicalizar(
            Entrada(processoDois, conformidade: new ResultadoConformidade(entradaDois, [])));
        JsonArray obrigatoriedadesDois = EnvelopeCodecRoundTripTests.Envelope(snapshotDois)
            ["documentosExigidos"]!["obrigatoriedades"]!.AsArray();

        BytesDoFragmento(obrigatoriedadesDois).Should().Equal(BytesDoFragmento(obrigatoriedadesUm),
            "duas entradas com o MESMO conjunto de regras em ordens DIFERENTES produzem o mesmo array canônico");
    }

    // ── documentosExigidos.obrigatoriedades[].predicado.args.codigos (ModalidadesMinimas) ──

    [Fact(DisplayName = "predicado.args.codigos (ModalidadesMinimas) ordena pela chave de conteúdo — oráculo escrito à mão")]
    public void PredicadoModalidadesMinimas_Codigos_OrdenaPelaChaveDeConteudo()
    {
        RegraAvaliada regraUm = RegraDeConformidade("REGRA-MM", Guid.CreateVersion7()) with
        {
            Predicado = new ModalidadesMinimas(["QUI", "AC", "LB_PPI"]),
        };

        ProcessoSeletivo processoUm = MontarProcesso();
        SnapshotCanonico snapshotUm = Canonicalizador.Canonicalizar(
            Entrada(processoUm, conformidade: new ResultadoConformidade([regraUm], [])));
        JsonArray codigosUm = EnvelopeCodecRoundTripTests.Envelope(snapshotUm)
            ["documentosExigidos"]!["obrigatoriedades"]![0]!["predicado"]!["args"]!["codigos"]!.AsArray();

        codigosUm.Select(static c => c!.GetValue<string>()).Should().Equal(
            ["AC", "LB_PPI", "QUI"],
            "'A' (0x41) < 'L' (0x4C) < 'Q' (0x51) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        RegraAvaliada regraDois = RegraDeConformidade("REGRA-MM", Guid.CreateVersion7()) with
        {
            Predicado = new ModalidadesMinimas(["AC", "LB_PPI", "QUI"]),
        };
        ProcessoSeletivo processoDois = MontarProcesso();
        SnapshotCanonico snapshotDois = Canonicalizador.Canonicalizar(
            Entrada(processoDois, conformidade: new ResultadoConformidade([regraDois], [])));
        JsonArray codigosDois = EnvelopeCodecRoundTripTests.Envelope(snapshotDois)
            ["documentosExigidos"]!["obrigatoriedades"]![0]!["predicado"]!["args"]!["codigos"]!.AsArray();

        BytesDoFragmento(codigosDois).Should().Equal(BytesDoFragmento(codigosUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }

    // ── documentosExigidos.obrigatoriedades[].predicado.args.necessidades (AtendimentoDisponivel) ──

    [Fact(DisplayName = "predicado.args.necessidades (AtendimentoDisponivel) ordena pela chave de conteúdo — oráculo escrito à mão")]
    public void PredicadoAtendimentoDisponivel_Necessidades_OrdenaPelaChaveDeConteudo()
    {
        RegraAvaliada regraUm = RegraDeConformidade("REGRA-AD", Guid.CreateVersion7()) with
        {
            Predicado = new AtendimentoDisponivel(["VISUAL", "AUDITIVA", "FISICA"]),
        };

        ProcessoSeletivo processoUm = MontarProcesso();
        SnapshotCanonico snapshotUm = Canonicalizador.Canonicalizar(
            Entrada(processoUm, conformidade: new ResultadoConformidade([regraUm], [])));
        JsonArray necessidadesUm = EnvelopeCodecRoundTripTests.Envelope(snapshotUm)
            ["documentosExigidos"]!["obrigatoriedades"]![0]!["predicado"]!["args"]!["necessidades"]!.AsArray();

        necessidadesUm.Select(static n => n!.GetValue<string>()).Should().Equal(
            ["AUDITIVA", "FISICA", "VISUAL"],
            "'A' (0x41) < 'F' (0x46) < 'V' (0x56) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        RegraAvaliada regraDois = RegraDeConformidade("REGRA-AD", Guid.CreateVersion7()) with
        {
            Predicado = new AtendimentoDisponivel(["FISICA", "AUDITIVA", "VISUAL"]),
        };
        ProcessoSeletivo processoDois = MontarProcesso();
        SnapshotCanonico snapshotDois = Canonicalizador.Canonicalizar(
            Entrada(processoDois, conformidade: new ResultadoConformidade([regraDois], [])));
        JsonArray necessidadesDois = EnvelopeCodecRoundTripTests.Envelope(snapshotDois)
            ["documentosExigidos"]!["obrigatoriedades"]![0]!["predicado"]!["args"]!["necessidades"]!.AsArray();

        BytesDoFragmento(necessidadesDois).Should().Equal(BytesDoFragmento(necessidadesUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }

    // ── documentosExigidos.metadadosFatos[].valoresDominio ──

    [Fact(DisplayName = "metadadosFatos[].valoresDominio ordena pela chave de conteúdo — oráculo escrito à mão")]
    public void MetadadosFatos_ValoresDominio_OrdenaPelaChaveDeConteudo()
    {
        Dictionary<string, MetadadoFatoCongelado> metadadosUm = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA", Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:COR_RACA",
                ValoresDominio: ["PRETA", "AMARELA", "BRANCA"], ValoresDominioDeclarados: null),
        };
        SnapshotCanonico snapshotUm = Canonicalizador.Canonicalizar(
            Entrada(MontarProcesso(), metadadosFatos: metadadosUm));
        JsonArray valoresUm = EnvelopeCodecRoundTripTests.Envelope(snapshotUm)
            ["documentosExigidos"]!["metadadosFatos"]![0]!["valoresDominio"]!.AsArray();

        valoresUm.Select(static v => v!.GetValue<string>()).Should().Equal(
            ["AMARELA", "BRANCA", "PRETA"],
            "'A' (0x41) < 'B' (0x42) < 'P' (0x50) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada invertida produz os MESMOS bytes.
        Dictionary<string, MetadadoFatoCongelado> metadadosDois = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA", Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:COR_RACA",
                ValoresDominio: ["BRANCA", "PRETA", "AMARELA"], ValoresDominioDeclarados: null),
        };
        SnapshotCanonico snapshotDois = Canonicalizador.Canonicalizar(
            Entrada(MontarProcesso(), metadadosFatos: metadadosDois));
        JsonArray valoresDois = EnvelopeCodecRoundTripTests.Envelope(snapshotDois)
            ["documentosExigidos"]!["metadadosFatos"]![0]!["valoresDominio"]!.AsArray();

        BytesDoFragmento(valoresDois).Should().Equal(BytesDoFragmento(valoresUm),
            "duas entradas com o MESMO conteúdo em ordens DIFERENTES produzem o mesmo array canônico");
    }
}
