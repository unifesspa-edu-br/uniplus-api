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
    /// Prova que o decoder NÃO reordena (ADR-0109 D9) com os seis arrays da issue #1067
    /// povoados ao mesmo tempo: <c>ocorrenciasEsperadas</c>,
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
    [Fact(DisplayName = "Round-trip com os seis conjuntos da issue #1067 povoados — bytes idênticos e a sequência imediatamente decodificada bate com o JSON")]
    public void RoundTrip_SeisConjuntosDaIssue1067Povoados_PreservaSequenciaImediatamenteDecodificada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Seis Conjuntos", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());


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
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
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
            modalidades: [modalidade], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: CorpusEnvelope.Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "INSCRICAO",
            origemData: OrigemDataFase.NoEdital,
            agrupaEtapas: true,
            permiteComplementacao: true,
            coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "INSCRICAO").Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormatosPermitidos formatosPermitidos = FormatosPermitidos.Criar(
            qualquer: false, entradas: [("PNG", null), ("JPEG", null), ("PDF", null)]).Value!;
        DocumentoExigido documento = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            [], [], null, formatosPermitidos, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
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
                Binding: "CAMPO_FORMULARIO:COR_RACA",
                ValoresDominio: ["ZETA_VALOR", "ALFA_VALOR"],
                ValoresDominioDeclarados: null),
        };

        DadosEdital dados = DadosEdital.Criar(
            numero: "099/2026",
            periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            documentoEditalId: Guid.CreateVersion7()).Value!;
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";

        EntradaCanonicalizacao entrada = new(
            processo, dados, hashDocumento, FusoInstitucional.ZoneId, Conformidade: conformidade, MetadadosFatosCongelados: metadadosFatos,
            ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        congelado.SchemaVersion.Should().Be("0.0.42", "pré-condição: o codec corrente emite a forma única");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "user-sub-seis-conjuntos", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao v1 = publicacao.Value!;

        JsonObject envelopeJson = Envelope(congelado);
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
        envelope.Grafo.NosExigencia.Single().OcorrenciasEsperadas.Should().Equal(
            ocorrenciasNoJson, "o decoder tem de preservar a sequência que leu — nunca recalculá-la");
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
                envelope.MetadadosFatosCongelados, envelope.ValoresSelecionaveisCongelados,
                TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)).Value!.Bytes;

        recodificado.Should().Equal(congelado.Bytes,
            "reidratar e recanonicalizar os seis conjuntos da issue #1067, todos povoados ao mesmo tempo, tem de " +
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
    /// Sem o ramo <c>True</c>/<c>False</c>, os campos booleanos mutados por este helper
    /// (<c>baseadoEmEnem</c>, <c>notaDeOrigemNoEnem</c>) virariam string JSON e seriam
    /// recusados por <see cref="LeitorEnvelope.Booleano"/>, que só aceita
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
}
