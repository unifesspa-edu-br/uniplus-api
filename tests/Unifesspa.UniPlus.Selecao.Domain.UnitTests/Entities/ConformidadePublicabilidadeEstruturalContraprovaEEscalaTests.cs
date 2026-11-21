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

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;

public sealed class ConformidadePublicabilidadeEstruturalContraprovaEEscalaTests
{
    [Fact(DisplayName = "Contraprova: fato coletável CONDICAO_ATENDIMENTO com condição ofertada mantém o item Ok")]
    public void Contraprova_FatoColetadoCondicaoAtendimentoComOferta_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirAtendimentoEspecializado(OfertaComCondicaoPcd()).IsSuccess.Should().BeTrue();
        FatoColetado fato = FatoColetadoDeTeste.Coletando("CONDICAO_ATENDIMENTO", 0)
            .ComRotulo("Você se enquadra em alguma condição de atendimento?")
            .DoTipo(TipoRenderizacao.SelecaoMultipla)
            .Construir();
        processo.DefinirItensComFaseDeInscricao([fato]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(
            i => i.Codigo == "fato_coletavel_sem_valores_ofertados" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: fato coletável renderizado como Booleano não aciona a conferência, mesmo sem oferta correspondente")]
    public void Contraprova_FatoColetadoBooleano_NaoAcionaAConferencia()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Mesmo código de fato da conferência, mas TipoRenderizacao fora de {SelecaoUnica, SelecaoMultipla}
        // — a conferência só se aplica a campo com opções, que é o que fica vazio sem oferta.
        FatoColetado fato = FatoColetadoDeTeste.Coletando("CONDICAO_ATENDIMENTO", 0).ComRotulo("Possui alguma condição de atendimento?").Construir();
        processo.DefinirItensComFaseDeInscricao([fato]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(
            i => i.Codigo == "fato_coletavel_sem_valores_ofertados" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static readonly string[] ItensDeReferenciaTemporal =
    [
        "referencia_temporal_ausente_com_gatilho_etario",
        "referencia_temporal_fase_fora_do_cronograma",
        "referencia_temporal_extremo_da_fase_ausente",
        "referencia_temporal_fim_inscricao_indisponivel",
    ];

    // Exigência GERAL, sem qualquer condição de gatilho — não aciona FAIXA_ETARIA.
    private static NoExigencia ExigenciaGeralDeIdentidade(Guid faseId) => NoExigencia.CriarFolha(
        DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!,
        0).Value!;

    [Fact(DisplayName = "Contraprova: ausência de gatilho por FAIXA_ETARIA mantém os quatro itens de referência temporal Ok, mesmo com outras exigências configuradas")]
    public void Contraprova_SemGatilhoPorFaixaEtaria_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirDocumentosExigidos([ExigenciaGeralDeIdentidade(processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id)])
            .IsSuccess.Should().BeTrue();

        IReadOnlyList<ItemConformidade> checklist = ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo));
        foreach (string item in ItensDeReferenciaTemporal)
        {
            checklist.Should().Contain(i => i.Codigo == item && i.Ok, $"sem gatilho por FAIXA_ETARIA — \"{item}\" não pode ficar vermelho");
        }

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Conferências que a raiz aplica antes do agregador — localidade e convenção de contagem
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static FaseCronograma FaseFinal(int ordem, string codigo = "RESULTADO_FINAL") => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false,
            RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "Convenção de contagem não declarada com fase que aceita recurso — item vermelho e Publicar recusa com AlgoritmoContagemPrazoNaoDeclarado")]
    public void ContagemDePrazos_AlgoritmoNaoDeclarado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        ContagemDePrazosDeTeste.RetirarConvencao(processo);

        SoEstesItensVermelhos(processo, ContagemDePrazosDeTeste.ComCalendarioVigente, "algoritmo_contagem_prazo_nao_declarado");

        Result<VersaoConfiguracao> resultado = Publicar(processo, ContagemDePrazosDeTeste.ComCalendarioVigente);
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

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.LocalidadeAusente");
    }

    private static FaseCronograma FasePreliminarDeGabaritoComConcluinte() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            2, Inicio: null, Fim: null, FaseConcluinteCodigo: "CONCLUSAO_DO_CICLO", UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: [ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    // Entrega as fases à lista do processo como a hidratação do EF faz, sem passar pela gravação do cronograma.
    private static void MaterializarFases(ProcessoSeletivo processo, params FaseCronograma[] fases) =>
        typeof(ProcessoSeletivo).GetField("_cronogramaFases", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(processo).Should().BeAssignableTo<List<FaseCronograma>>().Which
            .AddRange(fases);

    [Fact(DisplayName = "Fase que abre o ciclo com um tipo de ato e tem como concluinte a que só fecha outro tipo — item vermelho e Publicar recusa")]
    public void ConclusaoDoCiclo_ConcluinteComDefinitivoDeOutroTipo_ReprovaNoChecklistENaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        // O estado só existe em processo materializado do banco: a gravação do cronograma já
        // o recusa. Sem esta metade, a conferência de publicação ficaria sem prova.
        MaterializarFases(processo, FasePreliminarDeGabaritoComConcluinte(), FaseFinal(3, "CONCLUSAO_DO_CICLO"));

        SoEstesItensVermelhos(processo, "cronograma_conclusao_do_ciclo_recursal");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo");
    }

    // Duas pendências ao mesmo tempo, de conferências que Publicar() avalia em ordem conhecida: a convenção de contagem (2º) e a
    // taxa de inscrição, que é item estrutural e cai no agregador genérico (3º). Quem lê o checklist de cima para baixo
    // tem de encontrar a causa que efetivamente bloqueia antes da que só apareceria depois dela.
    private static ProcessoSeletivo ProcessoComContagemETaxaPendentes()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        ContagemDePrazosDeTeste.RetirarConvencao(processo);
        processo.DefinirTaxaInscricao(null!);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    [Fact(DisplayName = "Com duas pendências simultâneas, o checklist lista a que bloqueia a publicação antes da outra")]
    public void ContagemDePrazos_PrecedenciaDoChecklistSegueAsConferencias()
    {
        ProcessoSeletivo processo = ProcessoComContagemETaxaPendentes();

        string[] vermelhos = [.. ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(ContagemDePrazosDeTeste.ComCalendarioVigente, motivosEscolhidos: MotivosDoProcessoDeTeste.Cobrindo(processo)).ComRegrasDo(processo)).Where(static i => !i.Ok).Select(static i => i.Codigo)];

        // Equal, não BeEquivalentTo: aqui a ORDEM é o que está sendo provado.
        vermelhos.Should().Equal(
            ["algoritmo_contagem_prazo_nao_declarado", "taxa_inscricao_nao_declarada"],
            "a convenção de contagem é conferência anterior ao agregador de itens estruturais, e o " +
            "checklist reproduz a precedência de Publicar()");

        Result<VersaoConfiguracao> resultado = Publicar(processo, ContagemDePrazosDeTeste.ComCalendarioVigente);
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

    [Fact(DisplayName = "ENEM com cálculo local: resolução, quadro e grupo do quadro em toda oferta — os itens existem, nenhum vermelho, e Publicar conclui")]
    public void Enem_ProcessoCompletoPublica()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica), OfertaAmpla(5, GrupoHumanisticaI)]);

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Select(static i => i.Codigo).Should().Contain(
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
                EtapaProcesso.Criar(
                    new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                    new DeclaracaoDaAvaliacaoDaEtapa(Peso: 2m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
                EtapaDeProvaObjetiva("Entrevista", CaraterEtapa.Classificatoria, 2, 1m, null, "RESULTADO_FINAL"),
            ]).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static ConfiguracaoClassificacao ClassificacaoLocalSemEscala() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
        RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
        CasasArredondamento = 2,
        RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
        BaseadoEmEnem = true,
        ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
        QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
    }).Value!;

    [Fact(DisplayName = "Escala da nota final: cálculo local sem a escala — só o item da escala fica vermelho, e Publicar recusa")]
    public void EscalaDaNotaFinal_CalculoLocalSemEscala_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirClassificacao(ClassificacaoLocalSemEscala())
            .IsSuccess.Should().BeTrue("o rascunho aceita a classificação sem a escala");

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

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Select(static i => i.Codigo).Should().NotContain("classificacao_escala_nota_final_nao_declarada");
    }
}
