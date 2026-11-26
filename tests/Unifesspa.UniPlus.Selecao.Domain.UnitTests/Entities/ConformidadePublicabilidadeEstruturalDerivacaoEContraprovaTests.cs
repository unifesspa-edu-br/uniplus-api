namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDasCotasFederaisNaConformidade;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoEnemComCalculoLocal;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosItensAcimaDoTeto;

public sealed class ConformidadePublicabilidadeEstruturalDerivacaoEContraprovaTests
{
    private static readonly string[] ItensDeCascata =
    [
        "cascata_modalidade_fora_do_regime_federal",
        "cascata_origem_ausente",
        "cascata_fallback_nao_ofertado",
        "cascata_origem_nao_segue_cascata",
        "cascata_destino_desconhecido",
    ];

    [Fact(DisplayName = "Pré-canonicalização: o derivado booleano é escalar, e IGUAL a verdadeiro e a falso separa cota de ação afirmativa")]
    public void PreCanon_DerivadoBooleano_ProvaExclusao()
    {
        ProcessoSeletivo processo = CotaEAcaoAfirmativaPor("EGRESSO_ESCOLA_PUBLICA", true, false);
        ConfiguracaoDerivacaoFato egresso = ConfiguracaoDerivacaoFato.Criar(
            "EGRESSO_ESCOLA_PUBLICA", [RegraDerivacaoConfigurada.Criar(0, contribui: null, condicoes: null).Value!]).Value!;
        processo.DefinirRegrasDerivacao([egresso]).IsSuccess.Should().BeTrue();

        ItemDaExclusividadeOk(processo).Should().BeTrue();
    }

    [Fact(DisplayName = "Pré-canonicalização: 1 e 1.0 são o mesmo número e não separam cota de ação afirmativa")]
    public void PreCanon_NumeroEquivalente_NaoProvaExclusao() =>
        ItemDaExclusividadeOk(CotaEAcaoAfirmativaPor("NOTA", 1, 1.0m)).Should().BeFalse();

    /// <summary>
    /// As regras semeadas passam pela recusa: cada cláusula da ação afirmativa institucional recusa a
    /// opção pelas cotas que toda cota exige.
    /// </summary>
    [Fact(DisplayName = "Pré-canonicalização: cotas que exigem a opção e ação afirmativa institucional que a recusa não acendem o item")]
    public void PreCanon_OpcaoPelasCotasSeparaCotaDeAcaoAfirmativa_ItemOk() =>
        ItemDaExclusividadeOk(ProcessoComCotaEAcaoAfirmativa(
                Exige((OpcaoPelasCotasDaLei.Fato, true)), Exige((OpcaoPelasCotasDaLei.Fato, false), ("PCD", true))))
            .Should().BeTrue();

    /// <summary>
    /// A reserva para pessoa com deficiência se soma às cotas: a regra semeada dela não cita a opção
    /// pelas cotas e não acende o item.
    /// </summary>
    [Fact(DisplayName = "Pré-canonicalização: cotas que exigem a opção e AC_PCD que não a cita não acendem o item")]
    public void PreCanon_AcPcdSemCitarAOpcao_ItemOk() =>
        ItemDaExclusividadeOk(ProcessoComCotaEAcaoAfirmativa(
                Exige((OpcaoPelasCotasDaLei.Fato, true)), Exige(("PCD", true)), ModalidadesFederaisLei12711.AcPcd))
            .Should().BeTrue();

    [Fact(DisplayName = "Pré-canonicalização: ciclo entre duas configurações de derivação — item vermelho e Publicar recusa com GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo")]
    public void PreCanon_GrafoConjuntoComCiclo()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirRegrasDerivacao(DuasDerivacoesQueSeCitam()).IsSuccess.Should().BeTrue(
            "a definição isolada só barra código duplicado — o ciclo cruza duas configurações e passa aqui");

        AfirmarPendenciaUnicaERecusa(processo, "grafo_dependencia_com_ciclo", GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo);
    }

    [Fact(DisplayName = "Pré-canonicalização: desempate por área do ENEM sob classificação sem quadro de pesos — item vermelho e Publicar recusa nomeando o critério")]
    public void PreCanon_DesempatePorAreaSemQuadro()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // classificação importada: sem quadro de pesos por área
        ConfiguracaoDeclarada configuracao = ComCriterios(processo, [PorArea(3, "REDACAO")]);

        VermelhosDe(processo, configuracao).Should().BeEquivalentTo("desempate_area_enem_sem_quadro");

        Result<ConfiguracaoAptaACongelar> resultado = RecusaDe(processo, configuracao);
        resultado.Errors.Should().ContainSingle();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        resultado.Error.Message.Should().Contain("ordem 3");
    }

    [Fact(DisplayName = "Pré-canonicalização: formulário com mais itens que o teto — só o item do teto fica vermelho, e a conferência da publicação recusa")]
    public void PreCanon_ItensAcimaDoTeto()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        ConfiguracaoDeclarada configuracao = ComItensAcimaDoTeto(processo);
        CadastrosDaConfiguracao cadastros = CadastrosDeTeste.Para(processo);

        ChecklistDeConformidade.AvaliarConformidade(configuracao, cadastros).Where(static i => !i.Ok).Select(static i => i.Codigo)
            .Should().BeEquivalentTo("formulario_itens_em_excesso");
        CondicoesParaRegistrarConfiguracao.Causas(configuracao, cadastros)
            .Should().ContainSingle(static causa => causa.Error.Code == ItemFormularioErrorCodes.ItensEmExcesso);
    }

    private static ConfiguracaoDerivacaoFato[] DuasDerivacoesQueSeCitam()
    {
        CondicaoRegraDerivacao citaD2 = CondicaoRegraDerivacao.Criar(1, "D2", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        CondicaoRegraDerivacao citaD1 = CondicaoRegraDerivacao.Criar(1, "D1", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

        return
        [
            ConfiguracaoDerivacaoFato.Criar("D1", [RegraDerivacaoConfigurada.Criar(0, "X", [citaD2]).Value!]).Value!,
            ConfiguracaoDerivacaoFato.Criar("D2", [RegraDerivacaoConfigurada.Criar(0, "Y", [citaD1]).Value!]).Value!,
        ];
    }

    private static CriterioDesempate[] CriteriosComAreaRepetida() => [PorArea(1, "REDACAO"), PorArea(2, "MATEMATICA", "REDACAO")];

    private static CriterioDesempate PorArea(int ordem, params string[] areas) =>
        CriterioDesempate.Criar(ordem, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo(), 'e'), new ParametrosDesempateMaiorNotaAreaEnem(areas)).Value!;

    // A gravação recusa estes estados; a declaração montada à parte representa o que a conferência da publicação precisa cobrir.
    private static ConfiguracaoDeclarada ComCriterios(
        ProcessoSeletivo processo, IReadOnlyList<CriterioDesempate> criterios)
    {
        ConfiguracaoDeclarada raiz = processo.Declaracao();

        return new ConfiguracaoDeclarada(
            raiz.Identidade, raiz.Cronograma, new AvaliacaoDeclarada(raiz.Avaliacao.Etapas, criterios, raiz.Avaliacao.Classificacao),
            raiz.Vagas, raiz.Exigencias, raiz.Coleta, raiz.DivulgacaoETaxa)
        {
            ProvaPresencial = raiz.ProvaPresencial,
            MotivosDeDecisao = raiz.MotivosDeDecisao,
            Origem = raiz.Origem,
        };
    }

    // As regras dos critérios injetados na declaração não estão no processo, então entram no catálogo vigente do teste.
    private static CadastrosDaConfiguracao CadastrosDoProcesso(ProcessoSeletivo processo, ConfiguracaoDeclarada configuracao)
    {
        CadastrosDaConfiguracao padrao = CadastrosDeTeste.Com(motivosEscolhidos: MotivosDeDecisaoDoProcessoDeTeste.Cobrindo(processo)).ComRegrasDo(processo);
        Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> regras = new(padrao.Regras);
        foreach (ReferenciaRegra regra in configuracao.Avaliacao.CriteriosDesempate.Select(static c => c.Regra))
        {
            regras.TryAdd((regra.Codigo, regra.Versao), new RegraDoCatalogoCitada(default, regra, default));
        }

        return new CadastrosDaConfiguracao(padrao.ContagemDePrazos, padrao.FatosDeModalidade, padrao.TiposDeAtoUnicos, padrao.Corretores, padrao.Protecao, padrao.MotivosEscolhidos, padrao.FatosDasVersoesPublicadas)
        { Regras = regras, MunicipiosDoGeo = padrao.MunicipiosDoGeo, Precedencias = padrao.Precedencias, NomesDasFases = padrao.NomesDasFases };
    }

    private static string[] VermelhosDe(ProcessoSeletivo processo, ConfiguracaoDeclarada configuracao) =>
        [.. ChecklistDeConformidade.AvaliarConformidade(configuracao, CadastrosDoProcesso(processo, configuracao)).Where(static i => !i.Ok).Select(static i => i.Codigo)];

    private static Result<ConfiguracaoAptaACongelar> RecusaDe(ProcessoSeletivo processo, ConfiguracaoDeclarada configuracao) =>
        ConfiguracaoAptaACongelar.Conferir(processo.Id, configuracao, CadastrosDoProcesso(processo, configuracao));

    private static ConfiguracaoClassificacao ClassificacaoEnemPelaMediaPonderada() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = Regra(RegraDeCalculoDaNota.MediaPonderada.Codigo(), 'a'),
        RegraArredondamento = Regra(RegraDeArredondamentoDaNota.Truncar.Codigo(), 'b'),
        CasasArredondamento = 2,
        RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), 'c'),
        BaseadoEmEnem = true,
        ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
        QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        EscalaNotaFinal = 100m,
    }).Value!;

    private static ProcessoSeletivo ProcessoComClassificacaoEnemEEdicao()
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        processo.DefinirClassificacao(ClassificacaoEnemPelaMediaPonderada()).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!])
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    // Sem AtendimentoEspecializado, a conferência dos itens estruturais fica pendente; a fase sem coleta de inscrição, com
    // inscrição própria, deixa pendente a do cronograma.
    private static ProcessoSeletivo ProcessoComAtendimentoECronogramaPendentes()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Precedência", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirDistribuicaoVagas([DistribuicaoAmpla(10)], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada()).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseBase(coletaInscricao: false)], []).IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "Pré-canonicalização: área repetida entre critérios e classificação sem quadro — o checklist segue a precedência da conferência")]
    public void PreCanon_DesempatePorArea_PrecedenciaDoChecklistSegueAConferencia()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // classificação importada: sem quadro de pesos por área
        ConfiguracaoDeclarada configuracao = ComCriterios(processo, CriteriosComAreaRepetida());

        // Equal, não BeEquivalentTo: aqui a ORDEM é o que está sendo provado.
        VermelhosDe(processo, configuracao).Should().Equal(
            ["desempate_area_enem_citada_por_dois_criterios", "desempate_area_enem_sem_quadro"],
            "a conferência recusa primeiro a área repetida, e o checklist reproduz a precedência da conferência da publicação");
        RecusaDe(processo, configuracao).Error!.Code.Should().Be("ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio");
    }

    [Fact(DisplayName = "Pré-canonicalização: área citada por dois critérios de desempate — só o item da área repetida fica vermelho, e Publicar recusa")]
    public void PreCanon_DesempatePorAreaCitadaPorDoisCriterios()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnemEEdicao();
        ConfiguracaoDeclarada configuracao = ComCriterios(processo, CriteriosComAreaRepetida());

        VermelhosDe(processo, configuracao).Should().BeEquivalentTo("desempate_area_enem_citada_por_dois_criterios");

        Result<ConfiguracaoAptaACongelar> resultado = RecusaDe(processo, configuracao);
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio");
        resultado.Error.Message.Should().NotContain("REDACAO").And.Contain("ordem 1").And.NotContain("Áreas aceitas:");
    }

    [Fact(DisplayName = "Pré-canonicalização: desempate por área que cita área fora do quadro — só o item das áreas fica vermelho, e Publicar recusa")]
    public void PreCanon_DesempatePorAreaForaDoQuadro()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnemEEdicao();
        ConfiguracaoDeclarada configuracao = ComCriterios(processo, [PorArea(1, "FISICA")]);

        VermelhosDe(processo, configuracao).Should().BeEquivalentTo("desempate_area_enem_fora_do_quadro");

        Result<ConfiguracaoAptaACongelar> resultado = RecusaDe(processo, configuracao);
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
        resultado.Error.Message.Should().NotContain("FISICA").And.Contain("Áreas aceitas:");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Precedência: coletar todos os vereditos não pode mudar qual DomainError vem primeiro
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Precedência preservada: com a conferência dos itens estruturais e a do cronograma pendentes ao mesmo tempo, a conferência devolve a conformidade primeiro — mas o checklist mostra os DOIS itens pendentes")]
    public void Precedencia_ConferenciasSimultaneas_PublicarDevolveOPrimeiroNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoComAtendimentoECronogramaPendentes();

        IReadOnlyList<ItemConformidade> checklist = Checklist(processo);
        checklist.Should().Contain(i => i.Codigo == "atendimento_especializado_ausente" && !i.Ok,
            "a conferência dos itens estruturais também está pendente — o checklist projeta TODOS os vereditos, não só o primeiro");
        checklist.Should().Contain(i => i.Codigo == "cronograma_inscricao_propria_sem_fase_de_coleta" && !i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.Error!.Code.Should().Be(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            "PendenciaDeConformidade é checado ANTES de PendenciaDoCronograma em Publicar (e na sucessão de Retificar/Fechar) — " +
            "coletar todos os vereditos para o checklist não pode mudar essa ordem");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Contraprovas — estados VÁLIDOS que não podem virar falso vermelho
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Contraprova: processo estruturalmente conforme tem o checklist inteiramente verde E Publicar aceita (cronograma coerente, cascata não aplicável, sem gatilho etário)")]
    public void Contraprova_ProcessoConforme_ChecklistVerdeEPublica()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().OnlyContain(i => i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: SiSU com CLASSIFICACAO_IMPORTADA e SEM etapa é estado válido — os dois itens de coerência etapa×fase ficam Ok")]
    public void Contraprova_SiSU_ClassificacaoImportadaSemEtapa_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // ProcessoConforme já não define etapas nem fase que agrupa etapas — SiSU/importação
        // publica sem prova local. Esta contraprova torna essa premissa EXPLÍCITA.
        processo.Etapas.Should().BeEmpty("pré-condição da contraprova: sem etapa, sob CLASSIFICACAO_IMPORTADA");

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(
            i => i.Codigo == "cronograma_fase_agrupadora_sem_etapa_pontuada" && i.Ok);
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(
            i => i.Codigo == "cronograma_etapa_declara_a_fase" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: cascata não aplicável (nenhuma modalidade SegueCascata) mantém os cinco itens de cascata Ok")]
    public void Contraprova_CascataNaoAplicavel_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // modalidade única AC, null

        IReadOnlyList<ItemConformidade> checklist = Checklist(processo);
        foreach (string item in ItensDeCascata)
        {
            checklist.Should().Contain(i => i.Codigo == item && i.Ok, $"cascata não se aplica — \"{item}\" não pode ficar vermelho");
        }
    }
}
