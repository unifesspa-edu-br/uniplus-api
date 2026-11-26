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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDasCotasFederaisNaConformidade;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

public sealed class ConformidadePublicabilidadeEstruturalPreCanonicalizacaoTests
{
    private static FaseCronograma FaseDeHomologacao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "HOMOLOGACAO_INSCRICOES", OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            2, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static void ReferenciarOFimDaFase(ProcessoSeletivo processo, FaseCronograma fase) =>
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, fase.Id).Value!)
            .IsSuccess.Should().BeTrue();

    private static void ExigirDeclaracaoDeMaioridade(ProcessoSeletivo processo, Guid faseId)
    {
        DocumentoExigido exigencia = new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FormularioDeTeste.FormularioDaInscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [GatilhoFaixaEtaria()]))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [BaseLegalResolvida()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;
        processo.DefinirDocumentosExigidosNosFormulariosMinimos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!])
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Pré-canonicalização: referência temporal aponta para fase removida depois — item vermelho e Publicar recusa com ReferenciaTemporalFatosFaseInexistente")]
    public void PreCanon_ReferenciaTemporalFatosFaseInexistente()
    {
        ProcessoSeletivo processo = ProcessoComReferenciaAFaseRemovida();

        SoEstesItensVermelhos(processo, "referencia_temporal_fase_fora_do_cronograma");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente");
    }

    private static FaseCronograma FaseDeHomologacaoNaOrdem2() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "HOMOLOGACAO_INSCRICOES", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(2, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "Pré-canonicalização: referência temporal aponta para fase sem o extremo definido — item vermelho e Publicar recusa com ReferenciaTemporalFatosExtremoAusente")]
    public void PreCanon_ReferenciaTemporalFatosExtremoAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FaseCronograma faseSemExtremo = FaseDeHomologacaoNaOrdem2();
        processo.DefinirCronogramaFases([FaseBaseDaImportacao(), faseSemExtremo], [])
            .IsSuccess.Should().BeTrue();

        Guid faseComExigenciaId = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;
        ExigirDeclaracaoDeMaioridade(processo, faseComExigenciaId);

        ReferenciarOFimDaFase(processo, faseSemExtremo);

        SoEstesItensVermelhos(processo, "referencia_temporal_extremo_da_fase_ausente");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente");
    }

    [Fact(DisplayName = "Pré-canonicalização: FIM_INSCRICAO sem fase de coleta com Fim definido — item vermelho e Publicar recusa com ReferenciaTemporalFatosFimInscricaoIndisponivel")]
    public void PreCanon_ReferenciaTemporalFatosFimInscricaoIndisponivel()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        ExigirDeclaracaoDeMaioridade(processo, faseId);
        // FaseBase() não coleta inscrição — nenhuma fase resolve FIM_INSCRICAO.
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null).Value!)
            .IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo, "referencia_temporal_fim_inscricao_indisponivel");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel");
    }

    [Fact(DisplayName = "Pré-canonicalização: regra de derivação cita fato que o processo não coleta nem deriva — item vermelho e Publicar recusa com FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado")]
    public void PreCanon_RegraDeDerivacaoCitaFatoNaoColetado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        CondicaoRegraDerivacao condicao = CondicaoRegraDerivacao.Criar(
            1, "BOGUS", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        RegraDerivacaoConfigurada regra = RegraDerivacaoConfigurada.Criar(0, "X", [condicao]).Value!;
        ConfiguracaoDerivacaoFato configuracao = ConfiguracaoDerivacaoFato.Criar("D1", [regra]).Value!;
        processo.DefinirRegrasDerivacao([configuracao]).IsSuccess.Should().BeTrue(
            "a definição isolada só valida forma e unicidade — não conhece o universo de fatos do processo");

        SoEstesItensVermelhos(processo, "derivacao_fatos_citados_inexistentes");

        ExigirPublicacaoRecusada(processo, FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "Pré-canonicalização: fato coletável CONDICAO_ATENDIMENTO sem nenhuma condição ofertada — item vermelho e Publicar recusa com FatoColetadoSemValoresOfertados")]
    public void PreCanon_FatoColetadoCondicaoAtendimentoSemValoresOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // ProcessoConforme já oferta atendimento vazio (sem condições) — só falta o fato coletável.
        FatoColetado fato = FatoColetadoDeTeste.Coletando("CONDICAO_ATENDIMENTO", 0)
            .ComRotulo("Você se enquadra em alguma condição de atendimento?")
            .DoTipo(TipoDeCampo.EscolhaMultipla)
            .Construir();
        processo.DefinirItensNaFaseDaImportacao([fato]).IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    [Fact(DisplayName = "TIPO_DEFICIENCIA com os tipos do cadastro não depende do atendimento especializado: sem tipo ofertado, o processo publica")]
    public void FatoColetadoTipoDeficienciaDoCadastro_NaoDependeDoAtendimentoEspecializado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FatoColetado fato = FatoColetadoDeTeste.Coletando("TIPO_DEFICIENCIA", 0)
            .ComRotulo("Qual o tipo de deficiência?")
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComClassificacao("SENSIVEL")
            .Construir();
        processo.DefinirItensNaFaseDaImportacao([fato]).IsSuccess.Should().BeTrue();

        RegrasDasOpcoesDeclaradas.OrigemDasOpcoes(processo.Campos.Single(static c => c.FatoCodigo == "TIPO_DEFICIENCIA")).Should().Be(OrigemValoresColeta.Catalogo);
        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    /// <summary>
    /// Com a condição PcD ofertada (pré-requisito da ADR-0067) e nenhum tipo de deficiência, só o fato de tipo de
    /// deficiência fica sem valor para oferecer ao candidato.
    /// </summary>
    private static FatoColetado FatoDeTipoDeDeficienciaDoProcesso() => FatoColetadoDeTeste.Coletando("TIPO_DEFICIENCIA", 0)
        .ComRotulo("Qual o tipo de deficiência?")
        .DoTipo(TipoDeCampo.EscolhaUnica)
        .ComOrigem(OrigemValoresColeta.OpcoesDoProcesso)
        .Construir();

    [Fact(DisplayName = "TIPO_DEFICIENCIA com a origem do processo, copiada antes dos tipos do cadastro, segue com as opções da oferta: sem tipo ofertado, item vermelho e Publicar recusa")]
    public void PreCanon_FatoColetadoTipoDeficienciaDaOfertaSemValoresOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirAtendimentoEspecializado(OfertaComCondicaoPcd()).IsSuccess.Should().BeTrue();
        processo.DefinirItensNaFaseDaImportacao([FatoDeTipoDeDeficienciaDoProcesso()]).IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    private static FatoColetado CampoRecursoAcessibilidade() => FatoColetadoDeTeste.Coletando(AtendimentoEspecializado.FatoRecursoAcessibilidade, 0)
        .ComRotulo("De qual recurso de acessibilidade você vai precisar?")
        .DoTipo(TipoDeCampo.EscolhaMultipla)
        .ComClassificacao("SENSIVEL")
        .Construir();

    [Fact(DisplayName = "Pré-canonicalização: fato coletável RECURSO_ACESSIBILIDADE sem nenhum recurso ofertado — item vermelho e Publicar recusa com FatoColetadoSemValoresOfertados")]
    public void PreCanon_FatoColetadoRecursoAcessibilidadeSemValoresOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // ProcessoConforme já oferta atendimento vazio (sem recursos) — só falta o fato coletável.
        processo.DefinirItensNaFaseDaImportacao([CampoRecursoAcessibilidade()]).IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    [Fact(DisplayName = "Fato coletável RECURSO_ACESSIBILIDADE oferece os recursos do atendimento especializado, pelo código, e o processo publica")]
    public void FatoColetadoRecursoAcessibilidade_OpcoesSaoOsRecursosOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        OfertarSalaDeAmamentacaoELedor(processo);
        processo.DefinirItensNaFaseDaImportacao([CampoRecursoAcessibilidade()]).IsSuccess.Should().BeTrue();

        processo.OpcoesDoProcesso(AtendimentoEspecializado.FatoRecursoAcessibilidade)
            .Select(static o => (o.Codigo, o.Rotulo, o.Ordem))
            .Should().Equal(("LEDOR", "Ledor", 0), ("SALA_AMAMENTACAO", "Sala para amamentação", 1));

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Pré-canonicalização: cursos com conjuntos diferentes de cotas da lei — item vermelho e Publicar recusa")]
    public void PreCanon_CotasDaLeiDiferentesEntreOfertas()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaFederalCompleta(), DistribuicaoAmpla(10)]);
        processo.DefinirCascataRemanejamento(CascataCompleta()).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo, "modalidade_cotas_da_lei_diferentes_entre_ofertas");

        ExigirPublicacaoRecusada(processo, ProcessoSeletivoErrorCodes.CotasDaLeiDiferentesEntreOfertas);
    }

    /// <summary>
    /// A regra copiada sem a opção pelas cotas — a linha anterior à cópia da regra — dá cota e ação
    /// afirmativa a todo candidato: os dois itens acendem, e a publicação recusa pela forma da regra,
    /// que é o que pede gravar a distribuição de novo.
    /// </summary>
    [Fact(DisplayName = "Pré-canonicalização: regras que dão cota e ação afirmativa ao mesmo candidato — item vermelho e Publicar recusa")]
    public void PreCanon_CotaEAcaoAfirmativaDerivaveisJuntas()
    {
        ProcessoSeletivo processo = ProcessoComCotaEAcaoAfirmativa(PredicadoDnf.SemClausula, PredicadoDnf.SemClausula);

        SoEstesItensVermelhos(processo, "modalidade_regra_incoerente_com_a_natureza", "derivacao_cota_e_acao_afirmativa_juntas");

        ExigirPublicacaoRecusada(processo, ProcessoSeletivoErrorCodes.RegraIncoerenteComANatureza);
    }

    /// <summary>
    /// Sobre fato de seleção múltipla, <c>IGUAL</c> é pertinência: o candidato que marca os dois
    /// valores satisfaz as duas regras, então valores diferentes não as tornam excludentes.
    /// </summary>
    [Fact(DisplayName = "Pré-canonicalização: IGUAL a valores diferentes em fato de seleção múltipla não separa cota de ação afirmativa")]
    public void PreCanon_FatoMultivalorado_NaoProvaExclusao()
    {
        ProcessoSeletivo processo = CotaEAcaoAfirmativaPor("CONDICAO_ATENDIMENTO", "A", "B");
        FatoColetado condicao = FatoColetadoDeTeste.Coletando("CONDICAO_ATENDIMENTO", 0)
            .ComRotulo("Você se enquadra em alguma condição de atendimento?")
            .DoTipo(TipoDeCampo.EscolhaMultipla)
            .Construir();
        processo.DefinirItensNaFaseDaImportacao([condicao]).IsSuccess.Should().BeTrue();

        ItemDaExclusividadeOk(processo).Should().BeFalse();
    }

    private static void OfertarSalaDeAmamentacaoELedor(ProcessoSeletivo processo)
    {
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar(
                [],
                [RecursoOfertado.Criar(Guid.CreateVersion7(), "SALA_AMAMENTACAO", "Sala para amamentação"),
                 RecursoOfertado.Criar(Guid.CreateVersion7(), "LEDOR", "Ledor")],
                []).Value!).IsSuccess.Should().BeTrue();
    }

    private static ProcessoSeletivo ProcessoComReferenciaAFaseRemovida()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FaseCronograma faseComExigencia = FaseBaseDaImportacao();
        FaseCronograma faseAncora = FaseDeHomologacao();
        processo.DefinirCronogramaFases([faseComExigencia, faseAncora], [])
            .IsSuccess.Should().BeTrue();

        ExigirDeclaracaoDeMaioridade(processo, faseComExigencia.Id);

        ReferenciarOFimDaFase(processo, faseAncora);

        processo.DefinirCronogramaFases([faseComExigencia], [])
            .IsSuccess.Should().BeTrue();

        return processo;
    }

}
