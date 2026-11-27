namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os processos e as peças de configuração que os testes do painel de conformidade avaliam.</summary>
internal static class ProcessosDoPainelDeConformidade
{
    public static IProcessoSeletivoRepository RepositorioDo(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return repositorio;
    }

    /// <summary>O cronograma tem um produto definitivo cujo tipo de ato o catálogo declara único.</summary>
    public static ProcessoSeletivo QueDeclaraOTipoUnicoNoCronograma()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS 2026 — SiSU", OrigemCandidatos.ImportacaoExterna);
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
                OrigensDoTipoDeFaseDeTeste.Da(
                    Guid.CreateVersion7(), "CLASSIFICACAO", OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
                new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
                new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("ATO_UNICO", PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
                bancasRequeridas: []).Value!],
            []).IsSuccess.Should().BeTrue();

        return processo;
    }

    public static ProcessoSeletivo SoComAsEtapas()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS 2026 — SiSU", OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!]);

        return processo;
    }

    /// <summary>
    /// Todas as outras dimensões estão presentes; só falta a fase que coleta a inscrição, embora a origem seja própria.
    /// Sem ela, a pendência do cronograma que a publicação recusa precisa aparecer também no checklist: os dois lados concordam.
    /// </summary>
    public static ProcessoSeletivo DeInscricaoPropriaSemFaseDeColeta()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS 2026 — Falso Verde", OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([]).IsSuccess.Should().BeTrue(
            "Story #851 §3.5: lista vazia é estado válido sob CLASSIFICACAO_IMPORTADA — sem etapa, não precisamos de fase que agrupe etapas");
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoComAmpla()], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada()).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseQueProduzResultadoSemColetarInscricao()], []).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(TaxaSemCobranca()).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    /// <summary>Todas as dimensões obrigatórias configuradas: o checklist inteiro fica verde e a publicação aceita.</summary>
    public static ProcessoSeletivo ComTodosOsItensObrigatorios()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS 2026 — SiSU", OrigemCandidatos.InscricaoPropria);
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        processo.DefinirDistribuicaoVagas([DistribuicaoComAmpla()], FatosDeModalidadeDeTeste.DoCatalogo);
        processo.DefinirClassificacao(ClassificacaoPorMediaPonderada());
        processo.DefinirCronogramaFases([FaseConforme()], []);
        processo.DefinirEtapas([EtapaDaProvaObjetivaNoResultadoFinal()]);
        processo.DefinirTaxaInscricao(TaxaSemCobranca());
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        return processo;
    }

    /// <summary>Duas fases que produzem o mesmo resultado definitivo.</summary>
    public static ProcessoSeletivo ComODefinitivoEmDuasFases()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS 2026 — Aviso", OrigemCandidatos.ImportacaoExterna);
        processo.DefinirCronogramaFases(
            [FaseDoResultadoDaClassificacao(1, "CLASSIFICACAO"), FaseDoResultadoDaClassificacao(2, "HABILITACAO")],
            []).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>Processo conforme cuja única exigência documental é do tipo dado, comprovando os fatos dados.</summary>
    public static ProcessoSeletivo QueExigeODocumento(string nome, TipoDocumentoView tipo, IReadOnlyList<string>? fatosComprovados = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar(nome, out Guid faseId);
        DocumentoExigido exigencia = DocumentoExigidoDeTeste.Novo()
            .NaFase(faseId)
            .ComTipoDeOrigem(tipo.Id)
            .ComTipo(tipo.Codigo, tipo.Nome, tipo.Categoria)
            .NoFormulario(PapelDoFormularioDeTeste.Inscricao)
            .ComFatosComprovados(fatosComprovados)
            .Criar().Value!;
        Result definida = processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]);
        definida.IsSuccess.Should().BeTrue(definida.Error?.Message);

        return processo;
    }

    /// <summary>A publicação do processo, com o edital de sempre, para comparar com o que o painel diz.</summary>
    public static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) =>
        processo.Publicar(
            new CenarioDeCongelamento { Dados = DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!, Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", new string('a', 64)), AtorUsuarioContaId = "teste" });

    private static ConfiguracaoDistribuicaoVagas DistribuicaoComAmpla()
    {
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { RegraRemanejamento = null, BaseLegal = "base legal" }, 50).Value!;
        ReferenciaRegra regraInstitucional = ReferenciaRegra.De(
            RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", ProcessoSeletivoConformeBuilder.HashFixo);

        return ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regraInstitucional, Modalidades = [ampla] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
    }

    private static ConfiguracaoClassificacao ClassificacaoImportada() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", ProcessoSeletivoConformeBuilder.HashFixo),
        }).Value!;

    private static ConfiguracaoClassificacao ClassificacaoPorMediaPonderada() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.MediaPonderada.Codigo(), "v1", new string('b', 64)),
            RegraArredondamento = ReferenciaRegra.De(RegraDeArredondamentoDaNota.Truncar.Codigo(), "v1", new string('c', 64)),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            EscalaNotaFinal = 100m,
        }).Value!;

    // Produz resultado (satisfaz "vagas ofertadas"), mas não agrupa etapas (sem etapa, não precisa) e, sobretudo, não
    // coleta inscrição, embora a origem seja própria.
    private static FaseCronograma FaseQueProduzResultadoSemColetarInscricao() =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                origemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", origemData: OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false,
                coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(resultado: "RESULTADO_FINAL"),
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    private static FaseCronograma FaseConforme() =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                origemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", origemData: OrigemDataFase.NoEdital, agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: true,
                coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    private static FaseCronograma FaseDoResultadoDaClassificacao(int ordem, string codigo) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    // Publicar sem declarar a cobrança de taxa é recusado; declarada, ela isola a pendência que o teste observa.
    private static ConfiguracaoTaxaInscricao TaxaSemCobranca() =>
        ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!;

    private static EtapaProcesso EtapaDaProvaObjetivaNoResultadoFinal() =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
}
