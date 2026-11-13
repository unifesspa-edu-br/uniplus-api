namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class ConformidadePublicabilidadeEstruturalTests
{
    [Fact(DisplayName = "Pré-canonicalização: o derivado booleano é escalar, e IGUAL a verdadeiro e a falso separa cota de ação afirmativa")]
    public void PreCanon_DerivadoBooleano_ProvaExclusao()
    {
        ProcessoSeletivo processo = CotaEAcaoAfirmativaPor("EGRESSO_ESCOLA_PUBLICA", true, false);
        ConfiguracaoDerivacaoFato egresso = ConfiguracaoDerivacaoFato.Criar(
            "EGRESSO_ESCOLA_PUBLICA", [RegraDerivacaoConfigurada.Criar(0, contribui: null, condicoes: null).Value!]).Value!;
        processo.DefinirRegrasDerivacao([egresso], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ItemDaExclusividadeOk(processo).Should().BeTrue();
    }

    [Fact(DisplayName = "Pré-canonicalização: 1 e 1.0 são o mesmo número e não separam cota de ação afirmativa")]
    public void PreCanon_NumeroEquivalente_NaoProvaExclusao() =>
        ItemDaExclusividadeOk(CotaEAcaoAfirmativaPor("NOTA", 1, 1.0m)).Should().BeFalse();

    /// <summary>
    /// As regras semeadas passam pela recusa: cada cláusula de <c>AC_PCD</c> recusa a opção pelas
    /// cotas que toda cota exige.
    /// </summary>
    [Fact(DisplayName = "Pré-canonicalização: cotas que exigem a opção e AC_PCD que a recusa não acendem o item")]
    public void PreCanon_OpcaoPelasCotasSeparaCotaDeAcaoAfirmativa_ItemOk() =>
        ItemDaExclusividadeOk(ProcessoComCotaEAcaoAfirmativa(
                Exige((OpcaoPelasCotasDaLei.Fato, true)), Exige((OpcaoPelasCotasDaLei.Fato, false), ("PCD", true))))
            .Should().BeTrue();

    [Fact(DisplayName = "Pré-canonicalização: ciclo entre duas configurações de derivação — item vermelho e Publicar recusa com GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo")]
    public void PreCanon_GrafoConjuntoComCiclo()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        CondicaoRegraDerivacao citaD2 = CondicaoRegraDerivacao.Criar(1, "D2", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        CondicaoRegraDerivacao citaD1 = CondicaoRegraDerivacao.Criar(1, "D1", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        ConfiguracaoDerivacaoFato d1 = ConfiguracaoDerivacaoFato.Criar(
            "D1", [RegraDerivacaoConfigurada.Criar(0, "X", [citaD2]).Value!]).Value!;
        ConfiguracaoDerivacaoFato d2 = ConfiguracaoDerivacaoFato.Criar(
            "D2", [RegraDerivacaoConfigurada.Criar(0, "Y", [citaD1]).Value!]).Value!;
        processo.DefinirRegrasDerivacao([d1, d2], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "a definição isolada só barra código duplicado — o ciclo cruza duas configurações e passa aqui");

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("grafo_dependencia_com_ciclo");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo);
    }

    [Fact(DisplayName = "Pré-canonicalização: desempate por área do ENEM sob classificação sem quadro de pesos — item vermelho e Publicar recusa nomeando o critério")]
    public void PreCanon_DesempatePorAreaSemQuadro()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // classificação importada: sem quadro de pesos por área
        ConfiguracaoClassificacao classificacao = processo.Classificacao!;

        // As duas gravações recusam este estado; ele só chega por outro caminho — um envelope
        // restaurado ou uma escrita feita fora do agregado. É montado aqui direto no agregado:
        // o desempate gravado enquanto a classificação não existia, e a classificação sem
        // quadro posta depois.
        PropertyInfo propriedadeClassificacao = typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Classificacao))!;
        propriedadeClassificacao.SetValue(processo, null);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(3, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        propriedadeClassificacao.SetValue(processo, classificacao);

        SoEstesItensVermelhos(processo, "desempate_area_enem_sem_quadro");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        resultado.Error.Message.Should().Contain("ordem 3", "a recusa nomeia o critério que o operador tem de corrigir");
    }

    /// <summary>
    /// Processo conforme com uma etapa que compõe nota e a fase que a agrupa: sem elas o
    /// divisor da média é zero e a publicação recusa antes, por outro item. A oferta tem um
    /// grupo do quadro de teste, que a classificação ENEM com cálculo local exige.
    /// </summary>
    private static ProcessoSeletivo ProcessoConformeComEtapaQueCompoeNota()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao,
                agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
                produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null,
                emiteParecerIndividual: false,
                bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!],
            [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_FINAL").Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    [Fact(DisplayName = "Pré-canonicalização: mais critérios de desempate que o teto — só o item do teto fica vermelho, e Publicar recusa")]
    public void PreCanon_CriteriosDesempateAcimaDoTeto()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Direto na lista: a gravação e a restauração recusam acima do teto, e o gate cobre o
        // rascunho que chega por outro caminho.
        CriteriosDesempateDe(processo).AddRange(Enumerable.Range(1, RegrasDoDesempate.CriteriosDesempateMaximo + 1).Select(static ordem =>
            CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!));

        SoEstesItensVermelhos(processo, "criterios_desempate_em_excesso");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
    }

    [Theory(DisplayName = "Pré-canonicalização: desempate por área com a ordem de áreas mal formada — só o item da ordem fica vermelho, e Publicar recusa")]
    [InlineData(new string[0], "CriterioDesempate.AreasObrigatorias")]
    [InlineData(new[] { "redacao" }, "CriterioDesempate.AreaInvalida")]
    [InlineData(new[] { "REDACAO", "REDACAO" }, "CriterioDesempate.AreaRepetida")]
    public void PreCanon_DesempatePorAreaComOrdemMalFormada(string[] areas, string codigo)
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Classificacao))!.SetValue(processo, ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!);
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        // A ordem mal formada não passa por CriterioDesempate.Criar: os args são trocados depois,
        // como num critério que chega ao agregado por outro caminho.
        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!;
        typeof(CriterioDesempate).GetProperty(nameof(CriterioDesempate.Args))!.SetValue(criterio, new ArgsDesempateMaiorNotaAreaEnem(areas));
        CriteriosDesempateDe(processo).Add(criterio);

        SoEstesItensVermelhos(processo, "desempate_area_enem_areas_mal_formadas");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigo);
        resultado.Error.Message.Should().Contain("ordem 1");
    }

    [Fact(DisplayName = "Pré-canonicalização: formulário com mais itens que o teto — só o item do teto fica vermelho, e Publicar recusa")]
    public void PreCanon_ItensAcimaDoTeto()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        // Direto na lista: a gravação e a restauração recusam acima do teto, e o gate cobre o
        // rascunho que chega por outro caminho.
        ((List<FatoColetado>)typeof(ProcessoSeletivo).GetField("_campos", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(processo)!)
            .AddRange(Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1).Select(static i => FatoColetado.Criar(
                $"FATO_{i}", FormularioDeTeste.PrimeiraOrdemDeInscricao + i, "Campo", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
                etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!));

        SoEstesItensVermelhos(processo, "formulario_itens_em_excesso");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
    }

    private static List<CriterioDesempate> CriteriosDesempateDe(ProcessoSeletivo processo) =>
        (List<CriterioDesempate>)typeof(ProcessoSeletivo)
            .GetField("_criteriosDesempate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(processo)!;

    [Fact(DisplayName = "Pré-canonicalização: área repetida entre critérios e classificação sem quadro — o checklist segue a precedência do gate")]
    public void PreCanon_DesempatePorArea_PrecedenciaDoChecklistSegueOGate()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // classificação importada: sem quadro de pesos por área
        List<CriterioDesempate> criterios = (List<CriterioDesempate>)typeof(ProcessoSeletivo)
            .GetField("_criteriosDesempate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(processo)!;
        criterios.AddRange(
            CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!,
            CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["MATEMATICA", "REDACAO"])).Value!);

        string[] vermelhos = [.. ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Where(static i => !i.Ok).Select(static i => i.Codigo)];

        // Equal, não BeEquivalentTo: aqui a ORDEM é o que está sendo provado.
        vermelhos.Should().Equal(
            ["desempate_area_enem_citada_por_dois_criterios", "desempate_area_enem_sem_quadro"],
            "o gate recusa primeiro a área repetida, e o checklist reproduz a precedência de Publicar()");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio");
    }

    [Fact(DisplayName = "Pré-canonicalização: área citada por dois critérios de desempate — só o item da área repetida fica vermelho, e Publicar recusa")]
    public void PreCanon_DesempatePorAreaCitadaPorDoisCriterios()
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Classificacao))!.SetValue(processo, ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!);
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        // Direto na lista: a gravação recusa a área repetida, e o gate cobre o estado que
        // chega por outro caminho.
        List<CriterioDesempate> criterios = (List<CriterioDesempate>)typeof(ProcessoSeletivo)
            .GetField("_criteriosDesempate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(processo)!;
        criterios.AddRange(
            CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!,
            CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["MATEMATICA", "REDACAO"])).Value!);

        SoEstesItensVermelhos(processo, "desempate_area_enem_citada_por_dois_criterios");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio");
        resultado.Error.Message.Should().Contain("REDACAO").And.Contain("ordem 1").And.NotContain("Áreas aceitas:");
    }

    [Fact(DisplayName = "Pré-canonicalização: desempate por área que cita área fora do quadro — só o item das áreas fica vermelho, e Publicar recusa")]
    public void PreCanon_DesempatePorAreaForaDoQuadro()
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        PropertyInfo propriedadeClassificacao = typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Classificacao))!;
        propriedadeClassificacao.SetValue(processo, null);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["FISICA"])).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        propriedadeClassificacao.SetValue(processo, ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!);
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo, "desempate_area_enem_fora_do_quadro");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
        resultado.Error.Message.Should().Contain("FISICA").And.Contain("Áreas aceitas:");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Precedência: coletar todos os vereditos não pode mudar qual DomainError vem primeiro
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Precedência preservada: com Gate 1 (itens estruturais) e Gate 2 (cronograma) pendentes ao mesmo tempo, Publicar devolve ConformidadeInsuficiente primeiro — mas o checklist mostra os DOIS itens vermelhos")]
    public void Precedencia_GatesSimultaneos_PublicarDevolveOPrimeiroNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Precedência", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        // OfertaAtendimento NUNCA definida — Gate 1 (PendenciaDeConformidade) fica pendente.
        processo.DefinirDistribuicaoVagas([DistribuicaoAmpla(10)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Fase sem coleta de inscrição, com InscricaoPropria — Gate 2 (cronograma) TAMBÉM pendente.
        processo.DefinirCronogramaFases([FaseBase(coletaInscricao: false)], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IReadOnlyList<ItemConformidade> checklist = ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        checklist.Should().Contain(i => i.Codigo == "atendimento_especializado_ausente" && !i.Ok,
            "Gate 1 também está pendente — o checklist projeta TODOS os vereditos, não só o primeiro");
        checklist.Should().Contain(i => i.Codigo == "cronograma_inscricao_propria_sem_fase_de_coleta" && !i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
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

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().OnlyContain(i => i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: SiSU com CLASSIFICACAO-IMPORTADA e SEM etapa é estado válido — os dois itens de coerência etapa×fase ficam Ok (Story #851 §3.5)")]
    public void Contraprova_SiSU_ClassificacaoImportadaSemEtapa_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // ProcessoConforme já não define etapas nem fase que agrupa etapas — SiSU/importação
        // publica sem prova local. Esta contraprova torna essa premissa EXPLÍCITA.
        processo.Etapas.Should().BeEmpty("pré-condição da contraprova: sem etapa, sob CLASSIFICACAO-IMPORTADA");

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().Contain(
            i => i.Codigo == "cronograma_fase_agrupadora_sem_etapa_pontuada" && i.Ok);
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().Contain(
            i => i.Codigo == "cronograma_etapa_declara_a_fase" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: cascata não aplicável (nenhuma modalidade SegueCascata) mantém os cinco itens de cascata Ok")]
    public void Contraprova_CascataNaoAplicavel_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // modalidade única AC, RegraRemanejamentoModalidade.Nenhuma

        string[] itensDeCascata =
        [
            "cascata_modalidade_fora_do_regime_federal",
            "cascata_origem_ausente",
            "cascata_fallback_nao_ofertado",
            "cascata_origem_nao_segue_cascata",
            "cascata_destino_desconhecido",
        ];
        IReadOnlyList<ItemConformidade> checklist = ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        foreach (string item in itensDeCascata)
        {
            checklist.Should().Contain(i => i.Codigo == item && i.Ok, $"cascata não se aplica — \"{item}\" não pode ficar vermelho");
        }
    }
}
