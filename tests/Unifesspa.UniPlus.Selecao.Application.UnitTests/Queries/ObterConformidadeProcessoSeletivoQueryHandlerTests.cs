namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ObterConformidadeProcessoSeletivoQueryHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente retorna null (mapeado a 404 pelo controller)")]
    public async Task Handle_ProcessoInexistente_RetornaNull()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(Guid.CreateVersion7()), repository, Substitute.For<IAtoDoProcessoRepository>(), CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CadastrosVivos.CatalogoDeFatos(), Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<ITipoDocumentoReader>(), Substitute.For<IRegistroCodecsEnvelope>(), TimeProvider.System, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "Handle marca vermelho o tipo de ato único do cronograma que o ato da linhagem, ainda pendente em Publicações, já ocupa")]
    public async Task Handle_TipoUnicoOcupadoPeloRegistroLocal_ItemVermelho()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "CLASSIFICACAO", "CEPS", OrigemDataFase.NaExecucao,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: null, fim: null,
                produtos: [ProdutoDaFase.Criar("ATO_UNICO", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Guid atoDaLinhagem = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        repository.ObterAtosCriadoresAsync(processo.Id, Arg.Any<CancellationToken>()).Returns([atoDaLinhagem]);
        IAtoDoProcessoRepository atos = Substitute.For<IAtoDoProcessoRepository>();
        atos.ListarTiposDosAtosAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == atoDaLinhagem), Arg.Any<CancellationToken>())
            .Returns(["ATO_UNICO"]);
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new TipoAtoPublicadoView("ATO_UNICO", "Ato único", true, true, false, false)]);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, atos, CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CadastrosVivos.CatalogoDeFatos(), catalogo, Substitute.For<ITipoDocumentoReader>(), Substitute.For<IRegistroCodecsEnvelope>(), TimeProvider.System, CancellationToken.None);

        result!.Itens.Should().Contain(i => i.Codigo == "cronograma_tipo_de_ato_unico_declarado_uma_vez" && !i.Ok);
    }

    [Fact(DisplayName = "Handle com processo sem atendimento nem cronograma devolve os dois pendentes (Story #851 — Etapas não é mais item incondicional)")]
    public async Task Handle_EtapasSemAtendimento_ChecklistParcial()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 3m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, Substitute.For<IAtoDoProcessoRepository>(), CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CadastrosVivos.CatalogoDeFatos(), Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<ITipoDocumentoReader>(), Substitute.For<IRegistroCodecsEnvelope>(), TimeProvider.System, CancellationToken.None);

        result.Should().NotBeNull();
        // Etapa deixou de ser item do checklist (Story #851 §3.5): um processo sem prova
        // publica sem etapa quando o cronograma é coerente. A asserção é por prefixo do
        // código, e não pela frase que o item tinha, para continuar pegando a reintrodução
        // sob qualquer redação nova. O que continua obrigatório e ainda não satisfeito aqui
        // é atendimento, distribuição, classificação e cronograma de fases.
        // Os itens da escala das etapas existem sempre, mas sem etapa nenhum deles reprova.
        result!.Itens.Where(i => i.Codigo.StartsWith("etapa", StringComparison.Ordinal)).Should().OnlyContain(i => i.Ok);
        result.Itens.Should().Contain(i => i.Codigo == "atendimento_especializado_ausente" && !i.Ok);
        result.Itens.Should().Contain(i => i.Codigo == "cronograma_fases_ausente" && !i.Ok);
    }

    [Fact(DisplayName = "Handle com InscricaoPropria sem fase de coleta NÃO declara o checklist inteiramente verde (issue #1092 — regressão do falso verde)")]
    public async Task Handle_InscricaoPropriaSemFaseDeColeta_NaoDeclaraChecklistTodoVerde()
    {
        // Cenário da issue #1092: PendenciaDoCronograma tem quatro razões, mas antes da correção
        // só a de "inscrição própria sem fase de coleta" (via item genérico incondicional) nunca
        // aparecia no checklist — GET /conformidade devolvia tudo Ok enquanto POST /publicacao
        // recusava com 422. Todas as outras dimensões estão presentes; só falta a fase de coleta.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Falso Verde", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas([], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "Story #851 §3.5: lista vazia é estado válido sob CLASSIFICACAO-IMPORTADA — sem etapa, não precisamos de fase que agrupe etapas");
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "base legal", quantidadeDeclarada: 50).Value!;
        ReferenciaRegra regraInstitucional = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m, regraInstitucional, regraAjuste: null, referenciaDemografica: null, [ampla], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!,
            null, null,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)).Value!, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Fase que PRODUZ RESULTADO (satisfaz "vagas ofertadas"), mas NÃO agrupa etapas (não
        // precisa — sem etapa) e, sobretudo, NÃO coleta inscrição, embora a origem seja própria.
        FaseCronograma faseSemColeta = FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([faseSemColeta], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: declarada para isolar a pendência de cronograma como a ÚNICA vermelha.
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, Substitute.For<IAtoDoProcessoRepository>(), CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CadastrosVivos.CatalogoDeFatos(), Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<ITipoDocumentoReader>(), Substitute.For<IRegistroCodecsEnvelope>(), TimeProvider.System, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Itens.Should().Contain(
            i => i.Codigo == "cronograma_inscricao_propria_sem_fase_de_coleta" && !i.Ok,
            "a origem é InscricaoPropria e nenhuma fase coleta inscrição — antes da correção este item nem existia, e o checklist devolvia tudo Ok");
        // O processo também não tem formulário de inscrição, e os itens do formulário ficam fora da
        // razão que este teste isola.
        result.Itens.Where(static i => !i.Codigo.StartsWith("formulario_", StringComparison.Ordinal)).Should().ContainSingle(i => !i.Ok,
            "isolar a asserção: só esta razão deveria estar vermelha neste estado — as outras três razões do cronograma e os demais gates estão satisfeitos");

        // A mesma pendência que o checklist agora denuncia é a que Publicar já recusava — as
        // duas superfícies concordam depois da correção (a prova da bicondicional).
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!,
            "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", new string('a', 64), "teste", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta");
    }

    [Fact(DisplayName = "Handle com todos os itens obrigatórios configurados devolve checklist sem pendências, e a publicação aceita (bicondicional, issue #1092)")]
    public async Task Handle_TodosOsItens_SemPendencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 3m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "base legal", quantidadeDeclarada: 50).Value!;
        ReferenciaRegra regraInstitucional = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m, regraInstitucional, regraAjuste: null, referenciaDemografica: null, [ampla], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('b', 64)).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('c', 64)).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)).Value!,
            [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente);

        FaseCronograma faseConforme = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.NoEdital,
            agrupaEtapas: true,
            permiteComplementacao: false,
            coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente);

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, Substitute.For<IAtoDoProcessoRepository>(), CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CadastrosVivos.CatalogoDeFatos(), Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<ITipoDocumentoReader>(), Substitute.For<IRegistroCodecsEnvelope>(), TimeProvider.System, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Itens.Should().OnlyContain(i => i.Ok);

        // issue #1092 — a bicondicional exige mais do que "todas as dimensões presentes": o
        // checklist inteiramente verde TEM de significar que Publicar aceita. Fora do escopo
        // estrutural (não afetado aqui): conformidade legal, documento confirmado, tipo de ato.
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!,
            "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", new string('a', 64), "teste", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
    }

    [Fact(DisplayName = "Handle devolve os avisos em lista própria, fora dos itens do checklist")]
    public async Task Handle_DefinitivoRepetido_DevolveAvisoEmListaPropria()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Aviso", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma Fase(int ordem, string codigo) => FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([Fase(1, "CLASSIFICACAO"), Fase(2, "HABILITACAO")], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, Substitute.For<IAtoDoProcessoRepository>(), CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CadastrosVivos.CatalogoDeFatos(), Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<ITipoDocumentoReader>(), Substitute.For<IRegistroCodecsEnvelope>(), TimeProvider.System, CancellationToken.None);

        result!.Avisos.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new AvisoConformidadeDto(
                CodigosDeAvisoDeConformidade.CronogramaTipoDefinitivoEmMaisDeUmLugar,
                DimensaoConformidade.Cronograma,
                result.Avisos[0].Mensagem));
        result.Itens.Should().NotContain(i => i.Codigo == CodigosDeAvisoDeConformidade.CronogramaTipoDefinitivoEmMaisDeUmLugar);
    }

    [Fact(DisplayName = "Com versão publicada, o checklist usa a classificação congelada como a retificação: tipo congelado único e desmarcado no catálogo continua vermelho")]
    public async Task Handle_TipoCongeladoUnicoDesmarcadoNoCatalogo_ItemVermelho()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository, IRegistroCodecsEnvelope codecs) =
            ProcessoPublicadoCom(
                [ProdutoDaFase.Criar("ATO_X", PapelProdutoFase.Preliminar), ProdutoDaFase.Criar("ATO_X", PapelProdutoFase.Definitivo)],
                new TipoDeAtoCongelado("ATO_X", UnicoPorObjeto: true, EhResultado: true, CongelaConfiguracao: false));

        ConformidadeProcessoSeletivoDto result = await ChecklistAsync(
            processo, repository, codecs, new TipoAtoPublicadoView("ATO_X", "Ato X", false, UnicoPorObjeto: false, false, true));

        result.Itens.Should().Contain(i => i.Codigo == "cronograma_tipo_de_ato_unico_declarado_uma_vez" && !i.Ok);
    }

    [Fact(DisplayName = "Com versão publicada, o checklist usa a classificação congelada como a retificação: tipo congelado não congelante e marcado congelante no catálogo continua verde")]
    public async Task Handle_TipoCongeladoComumMarcadoCongelanteNoCatalogo_ItemVerde()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository, IRegistroCodecsEnvelope codecs) =
            ProcessoPublicadoCom(
                [ProdutoDaFase.Criar("ATO_X", PapelProdutoFase.Definitivo)],
                new TipoDeAtoCongelado("ATO_X", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));

        ConformidadeProcessoSeletivoDto result = await ChecklistAsync(
            processo, repository, codecs, new TipoAtoPublicadoView("ATO_X", "Ato X", CongelaConfiguracao: true, false, false, true));

        result.Itens.Should().Contain(i => i.Codigo == "cronograma_tipo_que_congela_nao_eh_resultado" && i.Ok);
    }

    [Fact(DisplayName = "Com versão publicada que o codec não lê, o checklist mostra a pendência em vez de avaliar pelo catálogo de hoje")]
    public async Task Handle_VersaoPublicadaIlegivel_ItemVermelho()
    {
        (ProcessoSeletivo processo, IProcessoSeletivoRepository repository, IRegistroCodecsEnvelope codecs) =
            ProcessoPublicadoCom(
                [ProdutoDaFase.Criar("ATO_X", PapelProdutoFase.Definitivo)],
                new TipoDeAtoCongelado("ATO_X", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
        codecs.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(
            Result<EnvelopeReidratado>.Failure(new DomainError("Envelope.SchemaNaoSuportado", "A versão do envelope não é suportada.")));

        ConformidadeProcessoSeletivoDto result = await ChecklistAsync(
            processo, repository, codecs, new TipoAtoPublicadoView("ATO_X", "Ato X", false, false, false, true));

        result.Itens.Should().Contain(i =>
            i.Codigo == ObterConformidadeProcessoSeletivoQueryHandler.CodigoDaVersaoPublicadaIlegivel
            && !i.Ok
            && i.Mensagem == "A versão do envelope não é suportada.");
    }

    private static Task<ConformidadeProcessoSeletivoDto?> ChecklistAsyncBruto(
        ProcessoSeletivo processo, IProcessoSeletivoRepository repository, IRegistroCodecsEnvelope codecs, TipoAtoPublicadoView hoje)
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(chamada => chamada.Arg<IReadOnlyCollection<string>>().Contains(hoje.Codigo) ? [hoje] : []);
        return ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, Substitute.For<IAtoDoProcessoRepository>(),
            CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CadastrosVivos.CatalogoDeFatos(), catalogo, Substitute.For<ITipoDocumentoReader>(), codecs,
            TimeProvider.System, CancellationToken.None);
    }

    private static async Task<ConformidadeProcessoSeletivoDto> ChecklistAsync(
        ProcessoSeletivo processo, IProcessoSeletivoRepository repository, IRegistroCodecsEnvelope codecs, TipoAtoPublicadoView hoje) =>
        (await ChecklistAsyncBruto(processo, repository, codecs, hoje))!;

    /// <summary>
    /// Processo com o cronograma pedido, cuja versão publicada congelou a classificação dada. O processo vivo
    /// declara o mesmo cronograma que a versão publicada.
    /// </summary>
    private static (ProcessoSeletivo Processo, IProcessoSeletivoRepository Repositorio, IRegistroCodecsEnvelope Codecs) ProcessoPublicadoCom(
        ProdutoDaFase[] produtos, TipoDeAtoCongelado congelado)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "CLASSIFICACAO", "CEPS", OrigemDataFase.NaExecucao,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: null, fim: null, produtos: produtos,
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id, [1], "0.0.38", "canonical-json/sha256@v1", Guid.CreateVersion7(), new string('a', 64), "sub", DateTimeOffset.UnixEpoch);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        repository.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versao);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            new GrafoConfiguracao(
                [.. processo.Etapas], OfertaAtendimentoEspecializado.Criar([], [], []).Value!, [.. processo.DistribuicaoVagas], processo.BonusRegional,
                [.. processo.CriteriosDesempate], ConfiguracaoClassificacao.Criar(
                    ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!, null, null,
                    ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)).Value!, [],
                    baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!,
                [.. processo.CronogramaFases], [.. processo.DocumentosExigidos], [], null,
                numeroDeOpcoesDeCurso: 1),
            DadosEdital.Criar("001/2027", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null,
            tiposDeAtoCongelados: new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal) { [congelado.Codigo] = congelado })));
        return (processo, repository, codecs);
    }
}
