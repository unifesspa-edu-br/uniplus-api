namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Confere a publicação e a configuração das consequências de grupos de comprovação.</summary>
public sealed class ProcessoSeletivoRegraDeComprovacaoConferenciasTests
{
    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Conferências de Grupo", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static FaseCronograma NovaFase(Guid faseCanonicaOrigemId, bool permiteComplementacao) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            faseCanonicaOrigemId, "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: permiteComplementacao, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static DocumentoExigido DocumentoQualquer(Guid faseId, FinalidadeFormulario? finalidade = null) => new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COD", "Nome", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, finalidade))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, null, []))
            .ComRequisitos(new RequisitosDoDocumento(Qualquer, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static BaseLegalDaComprovacao BaseLegal(StatusBaseLegal status) =>
        BaseLegalDaComprovacao.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, status, null).Value!;

    [Fact(DisplayName = "Grupo não pode retirar acréscimo que não é oferecido")]
    public void Grupo_AcrescimoNaoOferecido_Bloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoQualquer(fase.Id), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "RETIRA_O_ACRESCIMO"), 0, [BaseLegal(StatusBaseLegal.Resolvido)], [regraDeDocumento]).Value!;

        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo);

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ConsequenciasDoDescumprimento.AcrescimoNaoOferecido");
    }

    [Fact(DisplayName = "Grupo com consequência e base legal só PENDENTE reprova o verificação de conformidade")]
    public void Grupo_ComConsequenciaEBaseLegalPendente_ReprovaConformidade()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoQualquer(fase.Id), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "ELIMINA_DO_PROCESSO"), 0, [BaseLegal(StatusBaseLegal.Pendente)], [regraDeDocumento]).Value!;

        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Should().Contain(item => item.Codigo == "exigencias_base_legal_nao_resolvida" && !item.Ok);
    }

    [Fact(DisplayName = "Grupo com consequência e base legal RESOLVIDO aprova o verificação de conformidade")]
    public void Grupo_ComConsequenciaEBaseLegalResolvida_AprovaConformidade()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoQualquer(fase.Id), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "ELIMINA_DO_PROCESSO"), 0, [BaseLegal(StatusBaseLegal.Resolvido)], [regraDeDocumento]).Value!;

        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Should().Contain(item => item.Codigo == "exigencias_base_legal_nao_resolvida" && item.Ok);
    }

    [Fact(DisplayName = "Grupo não admite reenvio quando a fase não permite complementação")]
    public void Grupo_PendenciaReenvioSemComplementacao_RecusaForward()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: false);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoQualquer(fase.Id), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null, true), 0, [], [regraDeDocumento]).Value!;

        Result resultado = processo.DefinirDocumentosExigidos([grupo]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.PendenciaReenvioExigeComplementacao");
    }

    [Fact(DisplayName = "Fase com grupo que exige reenvio não pode deixar de permitir complementação")]
    public void Grupo_RetirarComplementacaoDeFaseComPendenciaReenvio_RecusaReverso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();
        FaseCronograma faseComComplementacao = NovaFase(faseCanonicaOrigemId, permiteComplementacao: true);
        processo.DefinirCronogramaFases([faseComComplementacao], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoQualquer(faseComComplementacao.Id), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null, true), 0, [], [regraDeDocumento]).Value!;
        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();

        FaseCronograma faseSemComplementacao = NovaFase(faseCanonicaOrigemId, permiteComplementacao: false);
        Result resultado = processo.DefinirCronogramaFases([faseSemComplementacao], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.PendenciaReenvioExigeComplementacao");
    }

    [Fact(DisplayName = "Regra de documento com mais de uma apresentação não impede a preparação da publicação")]
    public void RegraDeDocumentoSemGrupo_QuantidadeMinimaMaiorQueUm_NaoBloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoQualquer(fase.Id), 0, quantidadeMinima: 2).Value!;
        processo.DefinirDocumentosExigidos([regraDeDocumento]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo);

        pendencia.Should().BeNull(pendencia?.Message);
    }

    [Fact(DisplayName = "Regra de documento com critério de distinção não impede a preparação da publicação")]
    public void RegraDeDocumentoSemGrupo_ComCriterioDeDistincaoDasApresentacoes_NaoBloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(
            DocumentoQualquer(fase.Id), 0, quantidadeMinima: 1,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia).Value!;
        processo.DefinirDocumentosExigidos([regraDeDocumento]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo);

        pendencia.Should().BeNull(pendencia?.Message);
    }

    [Fact(DisplayName = "Regra de documento com uma apresentação e sem critério não impede a preparação da publicação")]
    public void RegraDeDocumentoSemGrupo_CardinalidadePadrao_NaoBloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoQualquer(fase.Id), 0).Value!;
        processo.DefinirDocumentosExigidos([regraDeDocumento]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo);

        pendencia.Should().BeNull();
    }

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: true, ColetaInscricao: true, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static void DefinirComposicaoFamiliarNaInscricao(ProcessoSeletivo processo, FaseCronograma fase)
    {
        GrupoColetado composicao = GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 0)
            .ComOcorrencias(1, 10)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(FatoColetadoDeTeste.Coletando("MAIOR_IDADE", 0).ComRotulo("Maior de idade").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir())
            .Construir();
        Result formulario = processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase.Id, null, FormularioDeTeste.Paginas());
        formulario.IsSuccess.Should().BeTrue(formulario.Error?.Message);
        processo.DefinirItens([], grupos: [composicao]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Regra de documento com repetição por grupo não impede a preparação da publicação")]
    public void RegraDeDocumentoSemGrupo_GrupoDoFormularioParaRepeticao_NaoBloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = FaseDeInscricao();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        DefinirComposicaoFamiliarNaInscricao(processo, fase);
        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(
            DocumentoQualquer(fase.Id, FinalidadeFormulario.Inscricao), 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;
        processo.DefinirDocumentosExigidos([regraDeDocumento]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo);

        pendencia.Should().BeNull(pendencia?.Message);
    }

    [Fact(DisplayName = "Grupo com itens aplicáveis não impede a preparação da publicação")]
    public void Grupo_ComItens_NaoBloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoQualquer(fase.Id), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "ELIMINA_DO_PROCESSO"), 0, [BaseLegal(StatusBaseLegal.Resolvido)], [regraDeDocumento]).Value!;
        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo);

        pendencia.Should().BeNull(pendencia?.Message);
    }
}
