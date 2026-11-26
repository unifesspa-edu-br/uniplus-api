namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Constrói o processo mínimo que passa no checklist de conformidade, para os testes que
/// precisam de um agregado publicável sem que a montagem seja o assunto deles.
/// </summary>
/// <remarks>
/// Compartilhado entre os testes de gate: cada gate recusa por uma razão, e provar isso exige
/// que todas as demais dimensões estejam satisfeitas — senão a recusa observada pode vir de
/// outro lugar. Manter uma única montagem evita que os arquivos divirjam quando o checklist
/// ganhar uma dimensão nova.
/// </remarks>
internal static class ProcessoConformeFactory
{
    internal static ReferenciaRegra Regra(string codigo, string hashSeed) =>
        ReferenciaRegra.De(codigo, "v1", new string(hashSeed[0], 64));

    /// <summary>
    /// Uma oferta de curso da distribuição: ampla concorrência única, com as vagas todas nela.
    /// Duas chamadas com ids distintos dão ao processo os dois cursos que a segunda opção exige.
    /// </summary>
    internal static ConfiguracaoDistribuicaoVagas Oferta(Guid? ofertaCursoOrigemId = null, PredicadoDnf? regraDeAplicabilidade = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(ofertaCursoOrigemId ?? Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = Regra(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "a"),
                Modalidades = [
                AmplaConcorrencia(regraDeAplicabilidade),
            ]
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static EtapaProcesso EtapaDaProvaObjetiva(string faseCodigo) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    /// <param name="declararTaxa">
    /// Falso deixa a taxa por declarar, único jeito de exercitar a recusa correspondente.
    /// </param>
    /// <param name="declararFada">Falso deixa o FADA por declarar.</param>
    /// <param name="comDesempate">Falso deixa a inscrição própria sem critério de desempate.</param>
    /// <param name="declararOpcoesDeCurso">Falso deixa o número de opções de curso por declarar.</param>
    /// <param name="fase">Fase do cronograma; a omissão usa a mínima e conforme, sem recurso.</param>
    internal static ProcessoSeletivo Criar(
        bool declararTaxa = true, FaseCronograma? fase = null, bool declararFada = true, bool comDesempate = true,
        bool declararOpcoesDeCurso = true, PredicadoDnf? regraDeAplicabilidade = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Gate", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        DefinirFaseEtapaEAtendimento(processo, fase);
        processo.DefinirDistribuicaoVagas([Oferta(regraDeAplicabilidade: regraDeAplicabilidade)], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        DefinirClassificacaoImportada(processo);
        DefinirTaxa(processo, declararTaxa);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo, declararFada, desempate: comDesempate, opcoesDeCurso: declararOpcoesDeCurso);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    /// <summary>
    /// Produto preliminar da fase conforme — a âncora que as regras de recurso montadas por
    /// esta suíte declaram.
    /// </summary>
    internal static readonly Guid ProdutoPreliminarId = new("77770000-0000-4000-8000-000000000001");

    /// <summary>
    /// Fase mínima e conforme: agrupa etapas (há uma), produz resultado, coleta inscrição (há
    /// vagas e a origem é inscrição própria) e homologa as inscrições, com a regra que aponta o resultado dela. A asserção de sucesso impede que uma fase recusada
    /// vire null e o teste siga com um cronograma vazio, passando por não exercitar nada.
    /// </summary>
    internal static FaseCronograma FaseConforme(
        RegraRecursoFase? regraRecurso = null, bool semUnidadeResponsavel = false, IReadOnlyList<BancaRequerida>? bancas = null)
    {
        Result<FaseCronograma> fase = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
                ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: semUnidadeResponsavel ? null : UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(ProdutosDaFaseConforme(comRecurso: regraRecurso is not null), regraRecurso),
            bancasRequeridas: bancas ?? BancasDaFaseConforme(regraRecurso));

        fase.IsSuccess.Should().BeTrue(fase.Error?.Message);
        return fase.Value!;
    }

    // A fase homologa as inscrições, e toda origem exige a banca de homologação das inscrições; a de recursos entra com o recurso.
    private static IReadOnlyList<BancaRequerida> BancasDaFaseConforme(RegraRecursoFase? regraRecurso) =>
        regraRecurso is null
            ? [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]
            : [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), BancasDeTeste.DeRecursosDaFase()];

    // O preliminar entra só quando há regra de recurso: é nele que a fase ancora o prazo, e sem
    // ele a factory devolveria uma fase recusada em vez da fase conforme que os testes desta
    // suíte tomam como ponto de partida. O Id é fixo para que quem monta a regra possa declarar
    // a âncora antes de a fase existir.
    private static ProdutoDaFase[] ProdutosDaFaseConforme(bool comRecurso) =>
        comRecurso
            ?
            [
                ProdutoDaFase.Reidratar(ProdutoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo),
            ]
            : [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)];

    internal static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static ModalidadeSelecionada AmplaConcorrencia(PredicadoDnf? regraDeAplicabilidade) => ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { BaseLegal = "Res. Unifesspa 532/2021", RegraDeAplicabilidade = regraDeAplicabilidade ?? PredicadoDnf.SemClausula }, 40).Value!;


    private static void DefinirClassificacaoImportada(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "b"),
            RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "c"),
        }).Value!).IsSuccess.Should().BeTrue();
    }


    private static void DefinirTaxa(ProcessoSeletivo processo, bool declararTaxa)
    {
        if (declararTaxa)
        {
            processo.DefinirTaxaInscricao(
                ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        }
    }


    private static void DefinirFaseEtapaEAtendimento(ProcessoSeletivo processo, FaseCronograma? fase)
    {
        FaseCronograma faseDoProcesso = fase ?? FaseConforme();
        processo.DefinirCronogramaFases([faseDoProcesso], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([EtapaDaProvaObjetiva(faseDoProcesso.Codigo)]).IsSuccess.Should().BeTrue();

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
    }

}
