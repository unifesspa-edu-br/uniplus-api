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
    [Fact(DisplayName = "Pré-canonicalização: referência temporal aponta para fase removida depois — item vermelho e Publicar recusa com ReferenciaTemporalFatosFaseInexistente")]
    public void PreCanon_ReferenciaTemporalFatosFaseInexistente()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Duas fases: a que fica com a exigência (sobrevive) e a que ancora a referência
        // temporal (será removida DEPOIS, deixando a referência órfã — §3.5-like, mas para
        // ReferenciaTemporalFatos, que DefinirCronogramaFases não guarda contra remoção).
        FaseCronograma faseComExigencia = FaseBase();
        FaseCronograma faseAncora = FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "HOMOLOGACAO", OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([faseComExigencia, faseAncora], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseComExigencia.Id, Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [GatilhoFaixaEtaria()], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseAncora.Id).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        // Remove a fase-âncora do cronograma — só faseComExigencia sobrevive (mesma
        // FaseCanonicaOrigemId, então DefinirDocumentosExigidos.ExigidoNaFaseId não fica órfão).
        processo.DefinirCronogramaFases([faseComExigencia], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("referencia_temporal_fase_fora_do_cronograma");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente");
    }

    [Fact(DisplayName = "Pré-canonicalização: referência temporal aponta para fase sem o extremo definido — item vermelho e Publicar recusa com ReferenciaTemporalFatosExtremoAusente")]
    public void PreCanon_ReferenciaTemporalFatosExtremoAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FaseCronograma faseSemExtremo = FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "HOMOLOGACAO", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([FaseBase(), faseSemExtremo], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        Guid faseComExigenciaId = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseComExigenciaId, Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [GatilhoFaixaEtaria()], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseSemExtremo.Id).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("referencia_temporal_extremo_da_fase_ausente");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente");
    }

    [Fact(DisplayName = "Pré-canonicalização: FIM_INSCRICAO sem fase de coleta com Fim definido — item vermelho e Publicar recusa com ReferenciaTemporalFatosFimInscricaoIndisponivel")]
    public void PreCanon_ReferenciaTemporalFatosFimInscricaoIndisponivel()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [GatilhoFaixaEtaria()], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // FaseBase() não coleta inscrição — nenhuma fase resolve FIM_INSCRICAO.
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("referencia_temporal_fim_inscricao_indisponivel");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel");
    }

    [Fact(DisplayName = "Pré-canonicalização: regra de derivação cita fato que o processo não coleta nem deriva — item vermelho e Publicar recusa com FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado")]
    public void PreCanon_RegraDeDerivacaoCitaFatoNaoColetado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        CondicaoRegraDerivacao condicao = CondicaoRegraDerivacao.Criar(
            1, "BOGUS", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        RegraDerivacaoConfigurada regra = RegraDerivacaoConfigurada.Criar(0, "X", [condicao]).Value!;
        ConfiguracaoDerivacaoFato configuracao = ConfiguracaoDerivacaoFato.Criar("D1", [regra]).Value!;
        processo.DefinirRegrasDerivacao([configuracao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "a definição isolada só valida forma e unicidade — não conhece o universo de fatos do processo");

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("derivacao_fatos_citados_inexistentes");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    private static OfertaAtendimentoEspecializado OfertaComCondicaoPcd() => OfertaAtendimentoEspecializado.Criar(
        [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
        [],
        []).Value!;

    [Fact(DisplayName = "Pré-canonicalização: fato coletável CONDICAO_ATENDIMENTO sem nenhuma condição ofertada — item vermelho e Publicar recusa com FatoColetadoSemValoresOfertados (cenário da issue)")]
    public void PreCanon_FatoColetadoCondicaoAtendimentoSemValoresOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // ProcessoConforme já oferta atendimento vazio (sem condições) — só falta o fato coletável.
        FatoColetado fato = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Você se enquadra em alguma condição de atendimento?",
            TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, precondicoes: null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItensComFaseDeInscricao([fato]).IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    [Fact(DisplayName = "TIPO_DEFICIENCIA com os tipos do cadastro não depende da oferta de atendimento: sem tipo ofertado, o processo publica")]
    public void FatoColetadoTipoDeficienciaDoCadastro_NaoDependeDaOfertaDeAtendimento()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FatoColetado fato = FatoColetado.Criar(
            "TIPO_DEFICIENCIA", 0, "Qual o tipo de deficiência?",
            TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, precondicoes: null, classificacaoProtecao: "SENSIVEL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItensComFaseDeInscricao([fato]).IsSuccess.Should().BeTrue();

        RegrasDasOpcoesDeclaradas.OrigemDasOpcoes(processo.Campos.Single(static c => c.FatoCodigo == "TIPO_DEFICIENCIA")).Should().Be(OrigemValoresColeta.Catalogo);
        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "TIPO_DEFICIENCIA com a origem do processo, copiada antes dos tipos do cadastro, segue com as opções da oferta: sem tipo ofertado, item vermelho e Publicar recusa")]
    public void PreCanon_FatoColetadoTipoDeficienciaDaOfertaSemValoresOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Condição PcD ofertada (pré-requisito da ADR-0067), mas nenhum TipoDeficiencia —
        // só o fato de tipo de deficiência fica sem valor para oferecer ao candidato.
        processo.DefinirOfertaAtendimento(OfertaComCondicaoPcd(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        FatoColetado fato = FatoColetado.Criar(
            "TIPO_DEFICIENCIA", 0, "Qual o tipo de deficiência?",
            TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, precondicoes: null, classificacaoProtecao: "PESSOAL",
            origemValores: OrigemValoresColeta.OpcoesDoProcesso, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItensComFaseDeInscricao([fato]).IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    private static FatoColetado CampoRecursoAcessibilidade() => FatoColetado.Criar(
        OfertaAtendimentoEspecializado.FatoRecursoAcessibilidade, 0, "De qual recurso de acessibilidade você vai precisar?",
        TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, precondicoes: null, classificacaoProtecao: "SENSIVEL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    [Fact(DisplayName = "Pré-canonicalização: fato coletável RECURSO_ACESSIBILIDADE sem nenhum recurso ofertado — item vermelho e Publicar recusa com FatoColetadoSemValoresOfertados")]
    public void PreCanon_FatoColetadoRecursoAcessibilidadeSemValoresOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // ProcessoConforme já oferta atendimento vazio (sem recursos) — só falta o fato coletável.
        processo.DefinirItensComFaseDeInscricao([CampoRecursoAcessibilidade()]).IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    [Fact(DisplayName = "Fato coletável RECURSO_ACESSIBILIDADE oferece os recursos da oferta de atendimento, pelo código, e o processo publica")]
    public void FatoColetadoRecursoAcessibilidade_OpcoesSaoOsRecursosOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [],
                [OfertaRecurso.Criar(Guid.CreateVersion7(), "SALA_AMAMENTACAO", "Sala para amamentação"),
                 OfertaRecurso.Criar(Guid.CreateVersion7(), "LEDOR", "Ledor")],
                []).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirItensComFaseDeInscricao([CampoRecursoAcessibilidade()]).IsSuccess.Should().BeTrue();

        processo.OpcoesDoProcesso(OfertaAtendimentoEspecializado.FatoRecursoAcessibilidade)
            .Select(static o => (o.Codigo, o.Rotulo, o.Ordem))
            .Should().Equal(("LEDOR", "Ledor", 0), ("SALA_AMAMENTACAO", "Sala para amamentação", 1));

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Pré-canonicalização: cursos com conjuntos diferentes de cotas da lei — item vermelho e Publicar recusa")]
    public void PreCanon_CotasDaLeiDiferentesEntreOfertas()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaFederalCompleta(), DistribuicaoAmpla(10)]);
        processo.DefinirCascataRemanejamento(CascataCompleta(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo, "modalidade_cotas_da_lei_diferentes_entre_ofertas");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.CotasDaLeiDiferentesEntreOfertas);
    }

    [Theory(DisplayName = "Pré-canonicalização: reserva de PcD de ampla concorrência com cota da lei exige a cota da lei para PcD")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void PreCanon_ReservaPcdSemCotaPcdDaLei(bool comCotaPcd, bool esperadoOk)
    {
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m,
            Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), regraAjuste: null, referenciaDemografica: null,
            [
                Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, 6),
                Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, RegraRemanejamentoModalidade.Nenhuma, 2),
                Modalidade(comCotaPcd ? "LI_PCD" : "LI_PPI", NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, 2,
                    regra: Exige((OpcaoPelasCotasDaLei.Fato, true))),
            ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        ProcessoSeletivo processo = ProcessoConforme([oferta]);

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Single(static i => i.Codigo == "modalidade_reserva_pcd_sem_cota_pcd_da_lei").Ok.Should().Be(esperadoOk);
    }

    /// <summary>
    /// As oito cotas da lei, a ampla e a ação afirmativa de PcD retirando duas vagas da ampla, cada uma
    /// com a regra de aplicabilidade dada.
    /// </summary>
    private static ProcessoSeletivo ProcessoComCotaEAcaoAfirmativa(PredicadoDnf regraDasCotas, PredicadoDnf regraDaAcaoAfirmativa)
    {
        ModalidadeSelecionada acPcd = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC_PCD", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiraDe,
            ModalidadesFederaisLei12711.Ac, RegraRemanejamentoModalidade.DestinoUnico, ModalidadesFederaisLei12711.Ac, null, null, regraDaAcaoAfirmativa, null, "base legal", 2).Value!;
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 0.5m, RegraLei12711(), RegraAjusteArt11(), Demografica(),
            [
                .. OrigensFederais.Select(codigo =>
                    Modalidade(codigo, NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, quantidadeDeclarada: null, regra: regraDasCotas)),
                Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, quantidadeDeclarada: null),
                acPcd,
            ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        processo.DefinirCascataRemanejamento(CascataCompleta(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
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

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RegraIncoerenteComANatureza);
    }

    private static bool ItemDaExclusividadeOk(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Single(i => i.Codigo == "derivacao_cota_e_acao_afirmativa_juntas").Ok;

    private static ProcessoSeletivo CotaEAcaoAfirmativaPor(string fato, object valorDaCota, object valorDaAcaoAfirmativa) =>
        ProcessoComCotaEAcaoAfirmativa(Exige((fato, valorDaCota)), Exige((fato, valorDaAcaoAfirmativa)));

    private static PredicadoDnf Exige(params (string Fato, object Valor)[] atomos) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. atomos.Select(static a => (0, CondicaoDnf.Criar(
            a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))]).Value!;

    /// <summary>
    /// Sobre fato de seleção múltipla, <c>IGUAL</c> é pertinência: o candidato que marca os dois
    /// valores satisfaz as duas regras, então valores diferentes não as tornam excludentes.
    /// </summary>
    [Fact(DisplayName = "Pré-canonicalização: IGUAL a valores diferentes em fato de seleção múltipla não separa cota de ação afirmativa")]
    public void PreCanon_FatoMultivalorado_NaoProvaExclusao()
    {
        ProcessoSeletivo processo = CotaEAcaoAfirmativaPor("CONDICAO_ATENDIMENTO", "A", "B");
        FatoColetado condicao = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Você se enquadra em alguma condição de atendimento?",
            TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, precondicoes: null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItensComFaseDeInscricao([condicao]).IsSuccess.Should().BeTrue();

        ItemDaExclusividadeOk(processo).Should().BeFalse();
    }
}
