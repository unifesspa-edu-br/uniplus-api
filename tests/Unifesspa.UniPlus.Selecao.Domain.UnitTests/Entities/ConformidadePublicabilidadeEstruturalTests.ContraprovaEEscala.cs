namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
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
    [Fact(DisplayName = "Contraprova: fato coletável CONDICAO_ATENDIMENTO com condição ofertada mantém o item Ok")]
    public void Contraprova_FatoColetadoCondicaoAtendimentoComOferta_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirOfertaAtendimento(OfertaComCondicaoPcd(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        FatoColetado fato = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Você se enquadra em alguma condição de atendimento?",
            TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, precondicoes: null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItensComFaseDeInscricao([fato]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().Contain(
            i => i.Codigo == "fato_coletavel_sem_valores_ofertados" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: fato coletável renderizado como Booleano não aciona o gate, mesmo sem oferta correspondente")]
    public void Contraprova_FatoColetadoBooleano_NaoAcionaGate()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Mesmo código de fato do gate, mas TipoRenderizacao fora de {SelecaoUnica, SelecaoMultipla}
        // — o gate só se aplica a campo com opções, que é o que fica vazio sem oferta.
        FatoColetado fato = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Possui alguma condição de atendimento?",
            TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, precondicoes: null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItensComFaseDeInscricao([fato]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().Contain(
            i => i.Codigo == "fato_coletavel_sem_valores_ofertados" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: ausência de gatilho por FAIXA_ETARIA mantém os quatro itens de referência temporal Ok, mesmo com outras exigências configuradas")]
    public void Contraprova_SemGatilhoPorFaixaEtaria_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        // Exigência GERAL, sem qualquer condição de gatilho — não aciona FAIXA_ETARIA.
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        string[] itensDeReferenciaTemporal =
        [
            "referencia_temporal_ausente_com_gatilho_etario",
            "referencia_temporal_fase_fora_do_cronograma",
            "referencia_temporal_extremo_da_fase_ausente",
            "referencia_temporal_fim_inscricao_indisponivel",
        ];
        IReadOnlyList<ItemConformidade> checklist = ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        foreach (string item in itensDeReferenciaTemporal)
        {
            checklist.Should().Contain(i => i.Codigo == item && i.Ok, $"sem gatilho por FAIXA_ETARIA — \"{item}\" não pode ficar vermelho");
        }

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Gates que a raiz aplica antes do agregador — localidade e convenção de contagem
    // ══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calendário vigente mínimo — um feriado nacional basta para o gate passar. Existe para que
    /// um teste sobre a convenção de contagem isole a convenção: sem ele, o processo com recurso
    /// teria duas pendências, e a asserção não distinguiria qual das duas o teste prova.
    /// </summary>
    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    private static ReferenciaRegra RegraDeRecursoAncorada() =>
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('d', 64)).Value!;

    /// <summary>Produto preliminar de <see cref="FaseComRecurso"/> — a âncora do prazo.</summary>
    private static readonly Guid ProdutoPreliminarId = new("77770000-0000-4000-8000-000000000004");

    private static RegraRecursoFase RecursoEmHoras() => RegraRecursoFase.Criar(
        RegraDeRecursoAncorada(),
        new ArgsRegraPrazoRecurso(
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            AdmiteInstanciaSuperior: false,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null),
        ProdutoPreliminarId).Value!;

    /// <summary>Fase preliminar que publica o ato âncora e aceita recurso sobre ele.</summary>
    private static FaseCronograma FaseComRecurso() => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", OrigemDataFase.NaExecucao,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
        produtos: [ProdutoDaFase.Reidratar(ProdutoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
        faseConcluinteCodigo: "RESULTADO_FINAL",
        emiteParecerIndividual: false,
        bancasRequeridas: [BancasDeTeste.DeRecursosDaFase()], regraRecurso: RecursoEmHoras(), unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static FaseCronograma FaseFinal(int ordem) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
        produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    [Fact(DisplayName = "Convenção de contagem não declarada com fase que aceita recurso — item vermelho e Publicar recusa com AlgoritmoContagemPrazoNaoDeclarado")]
    public void ContagemDePrazos_AlgoritmoNaoDeclarado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirCronogramaFases([FaseComRecurso(), FaseFinal(2)], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo, ComCalendario(), "algoritmo_contagem_prazo_nao_declarado");

        Result<VersaoConfiguracao> resultado = Publicar(processo, ComCalendario());
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado");
    }

    [Fact(DisplayName = "Localidade ausente — item vermelho e Publicar recusa com LocalidadeAusente")]
    public void ContagemDePrazos_LocalidadeAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        // A localidade é exigida desde a criação, então o fluxo público não alcança este
        // estado — o que se prova aqui é a defesa da raiz contra um caminho de escrita que a
        // contorne, incluindo a materialização de um registro gravado antes de o campo existir.
        // Sem essa prova, a projeção do item ficaria verde por construção e nunca exercitada.
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Localidade))!
            .SetValue(processo, null);

        SoEstesItensVermelhos(processo, "localidade_nao_declarada");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.LocalidadeAusente");
    }

    [Fact(DisplayName = "Fase que abre o ciclo com um tipo de ato e tem como concluinte a que só fecha outro tipo — item vermelho e Publicar recusa")]
    public void ConclusaoDoCiclo_ConcluinteComDefinitivoDeOutroTipo_ReprovaNoChecklistENaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        // O estado só existe em processo materializado do banco: a gravação do cronograma já
        // o recusa. Sem esta metade, o gate de publicação ficaria sem prova.
        FaseCronograma abre = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos: [ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar)],
            faseConcluinteCodigo: "RESULTADO_FINAL", emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        typeof(ProcessoSeletivo).GetField("_cronogramaFases", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(processo).Should().BeAssignableTo<List<FaseCronograma>>().Which
            .AddRange([abre, FaseFinal(2)]);

        SoEstesItensVermelhos(processo, "cronograma_conclusao_do_ciclo_recursal");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo");
    }

    [Fact(DisplayName = "Com duas pendências simultâneas, o checklist lista a que bloqueia a publicação antes da outra")]
    public void ContagemDePrazos_PrecedenciaDoChecklistSegueOsGates()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Duas pendências ao mesmo tempo, de gates que Publicar() avalia em ordem conhecida:
        // a convenção de contagem (2º) e a taxa de inscrição, que é item estrutural e cai no
        // agregador genérico (3º). Quem lê o checklist de cima para baixo tem de encontrar a
        // causa que efetivamente bloqueia antes da que só apareceria depois dela.
        processo.DefinirCronogramaFases([FaseComRecurso(), FaseFinal(2)], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(null!, PrecondicaoIfMatch.Curinga);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        string[] vermelhos = [.. ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ComCalendario(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Where(static i => !i.Ok).Select(static i => i.Codigo)];

        // Equal, não BeEquivalentTo: aqui a ORDEM é o que está sendo provado.
        vermelhos.Should().Equal(
            ["algoritmo_contagem_prazo_nao_declarado", "taxa_inscricao_nao_declarada"],
            "a convenção de contagem é gate anterior ao agregador de itens estruturais, e o " +
            "checklist reproduz a precedência de Publicar()");

        Result<VersaoConfiguracao> resultado = Publicar(processo, ComCalendario());
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
            "a publicação recusa pela primeira causa da mesma precedência");
    }

    [Fact(DisplayName = "Contraprova: sem fase que aceite recurso, a convenção de contagem não é exigida")]
    public void ContagemDePrazos_SemRecursoNaoExigeConvencao()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Processo baseado em ENEM com cálculo local: resolução, quadro e grupo das ofertas
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static readonly (string? Codigo, string? Rotulo) GrupoHumanisticaI = ("HUMANISTICA_I", "Humanística I");

    private static ConfiguracaoClassificacao ClassificacaoEnemLocal(IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro) =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: quadro, escalaNotaFinal: 100m).Value!;

    private static ProcessoSeletivo ProcessoEnemLocal(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas,
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro = null)
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        processo.DefinirDistribuicaoVagas(ofertas, FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoEnemLocal(quadro ?? QuadroPesoAreaEnemDeTeste.Completo()), PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        // Classificação baseada em ENEM exige ao menos uma edição admitida (issue #1620) — sem
        // ela, o item "classificacao_edicao_enem_nao_declarada" ficaria vermelho e interferiria
        // nos testes desta classe, que focam nos itens do quadro de pesos por área.
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    /// <summary>A fábrica recusa quadro vazio; o EF hidrata a linha sem passar por ela.</summary>
    private static void EsvaziarQuadro(ConfiguracaoClassificacao classificacao) =>
        ((List<GrupoPesoAreaEnemCongelado>)typeof(ConfiguracaoClassificacao)
            .GetField("_quadroPesoAreaEnem", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(classificacao)!).Clear();

    private static ItemConformidade Item(ProcessoSeletivo processo, string codigo) =>
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Single(i => i.Codigo == codigo);

    [Fact(DisplayName = "ENEM com cálculo local: resolução, quadro e grupo do quadro em toda oferta — os itens existem, nenhum vermelho, e Publicar conclui")]
    public void Enem_ProcessoCompletoPublica()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica), OfertaAmpla(5, GrupoHumanisticaI)]);

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Select(static i => i.Codigo).Should().Contain(
            ["classificacao_resolucao_peso_area_enem_ausente", "distribuicao_vagas_oferta_sem_grupo_area_enem", "classificacao_grupo_area_enem_da_oferta_fora_do_quadro", "desempate_area_enem_sem_quadro"],
            "a exigência existe, e o item existe com ela; o do desempate sem quadro some só quando a resolução está pendente");
        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Escala da nota final: o ENEM combinado com etapa própria que tem nota publica quando a classificação declara a escala")]
    public void EscalaDaNotaFinal_EnemComEtapaPropria_Publica()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirEtapas(
            [
                EtapaProcesso.Criar("Nota do ENEM", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, peso: 2m, ordem: 1, faseCodigo: "RESULTADO_FINAL").Value!,
                EtapaProcesso.Criar("Entrevista", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 2, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_FINAL").Value!,
            ],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Escala da nota final: cálculo local sem a escala — só o item da escala fica vermelho, e Publicar recusa")]
    public void EscalaDaNotaFinal_CalculoLocalSemEscala_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: true,
                resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: null).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue("o rascunho aceita a classificação sem a escala");

        SoEstesItensVermelhos(processo, "classificacao_escala_nota_final_nao_declarada");
        Item(processo, "classificacao_escala_nota_final_nao_declarada").Dimensao.Should().Be(DimensaoConformidade.Classificacao);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain("Escala da nota final");
    }

    [Fact(DisplayName = "Escala da nota final: classificação importada não tem o item, porque o resultado vem pronto de fora")]
    public void EscalaDaNotaFinal_ClassificacaoImportada_SemItem()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Select(static i => i.Codigo).Should().NotContain("classificacao_escala_nota_final_nao_declarada");
    }

    public enum FaltaNaResolucao
    {
        ResolucaoAusente,
        ResolucaoEmBranco,
        QuadroVazio,
    }
}
