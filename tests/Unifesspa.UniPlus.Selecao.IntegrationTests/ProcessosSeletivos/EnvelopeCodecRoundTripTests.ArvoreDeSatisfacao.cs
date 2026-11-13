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
    /// Prova de fidelidade do codec sobre a MESMA exigência documental rica que a golden
    /// fixture do canonicalizador congela
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
            ValoresSelecionaveisCongelados: valoresSelecionaveis,
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);
        SnapshotCanonico congelado = new EnvelopeCodec().Codificar(entrada);
        congelado.SchemaVersion.Should().Be("0.0.42", "pré-condição: o codec corrente emite a forma única");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            entrada.Dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            entrada.HashDocumento, "user-sub-123", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
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
                reidratado.Value.ValoresSelecionaveisCongelados,
                reidratado.Value.CalendarioDiasUteis, reidratado.Value.AgregadosDosGrupos, reidratado.Value.ApresentacaoDosPressupostos,
                TiposDeAtoCongelados: reidratado.Value.TiposDeAtoCongelados, FatosDerivadosCongelados: reidratado.Value.FatosDerivadosCongelados)).Value!.Bytes;

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Árvore 1.4", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());


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
                    regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
                    acaoQuandoIndeferido: null,
                    baseLegal: "Res. Unifesspa 532/2021",
                    quantidadeDeclarada: 40).Value!,
            ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
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

        // obrigatorio: false — DocumentoExigido.DeterminaResultado() (obrigatória OU
        // consequência declarada) fica falso nas duas folhas, então a checagem de base
        // legal de AvaliarConformidade não exige NoExigenciaBaseLegal delas: só a
        // consequência ELIMINA do GRUPO determina resultado aqui, e a base legal própria
        // dele (baseLegalDoGrupo, abaixo) já cobre isso.
        FormatosPermitidos qualquer = FormatosPermitidos.Criar(true, null).Value!;
        DocumentoExigido rg = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null, qualquer, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
        // A declaração de renda vem com o modelo editável que o candidato preenche — o decoder tem
        // de remontá-lo, senão a recanonicalização o perderia.
        DocumentoExigido comprovanteRenda = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "COMPROVANTE_RENDA", "Comprovante de renda", "SOCIOECONOMICO", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null, qualquer, null,
            modelo: new ModeloDaExigencia(Guid.CreateVersion7(), "Declaração de renda.docx", FormatoDeDocumento.Docx, new string('e', 64)), finalidade: FinalidadeFormulario.Inscricao).Value!;

        NoExigencia folhaRg = NoExigencia.CriarFolha(
            rg, 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal,
            dataReferencia: new DateOnly(2026, 3, 31)).Value!;
        processo.DefinirItens([], grupos:
        [
            GrupoColetado.Criar(
                "COMPOSICAO_FAMILIAR", 0, FormularioDeTeste.Secao, "Composição familiar", 1, 10, null, Obrigatoriedade.Sempre,
                [FatoColetado.Criar("SEM_RENDA", 0, "Sem renda", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!]).Value!,
        ]).IsSuccess.Should().BeTrue();
        NoExigencia folhaRenda = NoExigencia.CriarFolha(
            comprovanteRenda, 1, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;
        NoExigenciaBaseLegal baseLegalDoGrupo = NoExigenciaBaseLegal.Criar(
            "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, "Norma interna").Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [baseLegalDoGrupo], [folhaRg, folhaRenda]).Value!;

        processo.DefinirDocumentosExigidos([raiz], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DadosEdital dados = DadosEdital.Criar(
            numero: "099/2026",
            periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            documentoEditalId: Guid.CreateVersion7()).Value!;
        const string hashDocumento = "2222222222222222222222222222222222222222222222222222222222222222";

        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, hashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
        congelado.SchemaVersion.Should().Be("0.0.42", "pré-condição: o codec corrente emite a forma única");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "user-sub-arvore", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao v1 = publicacao.Value!;

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(v1);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        // A restauração reaproveita a exigência viva de mesmo Id, então a recanonicalização abaixo
        // não enxerga o que o decoder perde numa exigência: o modelo é conferido no grafo.
        reidratado.Value!.Grafo.DocumentosExigidos.Single(static d => d.TipoDocumentoCodigo == "COMPROVANTE_RENDA").Modelo
            .Should().Be(comprovanteRenda.Modelo);

        processo.RestaurarConfiguracaoCongelada(v1, reidratado.Value.Grafo).IsSuccess.Should().BeTrue();

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
        folhaRendaRecarregada.RepetePorEntidade.Should().Be("COMPOSICAO_FAMILIAR");
        folhaRendaRecarregada.DocumentoExigido.Should().NotBeNull();
        folhaRendaRecarregada.DocumentoExigido!.TipoDocumentoCodigo.Should().Be("COMPROVANTE_RENDA");
    }

    // ── issue #1067 — os seis conjuntos ordenados por conteúdo, povoados no mesmo envelope ──
}
