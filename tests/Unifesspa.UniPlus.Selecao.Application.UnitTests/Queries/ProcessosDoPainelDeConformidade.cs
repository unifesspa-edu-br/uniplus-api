namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
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
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "CLASSIFICACAO", OrigemDataFase.NaExecucao,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: null, fim: null,
                produtos: [ProdutoDaFase.Criar("ATO_UNICO", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    public static ProcessoSeletivo SoComAsEtapas()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS 2026 — SiSU", OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 3m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!], PrecondicaoIfMatch.Ausente);

        return processo;
    }

    /// <summary>
    /// Todas as outras dimensões estão presentes; só falta a fase que coleta a inscrição, embora a origem seja própria.
    /// Sem ela, a pendência do cronograma que a publicação recusa precisa aparecer também no checklist: os dois lados concordam.
    /// </summary>
    public static ProcessoSeletivo DeInscricaoPropriaSemFaseDeColeta()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS 2026 — Falso Verde", OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "Story #851 §3.5: lista vazia é estado válido sob CLASSIFICACAO-IMPORTADA — sem etapa, não precisamos de fase que agrupe etapas");
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoComAmpla()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseQueProduzResultadoSemColetarInscricao()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(TaxaSemCobranca(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    /// <summary>Todas as dimensões obrigatórias configuradas: o checklist inteiro fica verde e a publicação aceita.</summary>
    public static ProcessoSeletivo ComTodosOsItensObrigatorios()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS 2026 — SiSU", OrigemCandidatos.InscricaoPropria);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([DistribuicaoComAmpla()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ClassificacaoPorMediaPonderada(), PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente);
        processo.DefinirEtapas([EtapaDaProvaObjetivaNoResultadoFinal()], PrecondicaoIfMatch.Ausente);
        processo.DefinirTaxaInscricao(TaxaSemCobranca(), PrecondicaoIfMatch.Ausente);
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
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>Processo conforme cuja única exigência documental é do tipo dado, comprovando os fatos dados.</summary>
    public static ProcessoSeletivo QueExigeODocumento(string nome, TipoDocumentoView tipo, IReadOnlyList<string>? fatosComprovados = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar(nome, out Guid faseId);
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, tipo.Id, tipo.Codigo, tipo.Nome, tipo.Categoria, ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null,
            finalidade: FinalidadeFormulario.Inscricao, fatosComprovados: fatosComprovados).Value!;
        Result definida = processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente);
        definida.IsSuccess.Should().BeTrue(definida.Error?.Message);

        return processo;
    }

    /// <summary>A publicação do processo, com o edital de sempre, para comparar com o que o painel diz.</summary>
    public static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) =>
        processo.Publicar(
            DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!,
            "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", new string('a', 64), "teste", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    private static ConfiguracaoDistribuicaoVagas DistribuicaoComAmpla()
    {
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "base legal", quantidadeDeclarada: 50).Value!;
        ReferenciaRegra regraInstitucional = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!;

        return ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m, regraInstitucional, regraAjuste: null, referenciaDemografica: null, [ampla], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
    }

    private static ConfiguracaoClassificacao ClassificacaoImportada() =>
        ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!,
            null, null,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)).Value!, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!;

    private static ConfiguracaoClassificacao ClassificacaoPorMediaPonderada() =>
        ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('b', 64)).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('c', 64)).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)).Value!,
            [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!;

    // Produz resultado (satisfaz "vagas ofertadas"), mas não agrupa etapas (sem etapa, não precisa) e, sobretudo, não
    // coleta inscrição, embora a origem seja própria.
    private static FaseCronograma FaseQueProduzResultadoSemColetarInscricao() =>
        FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL",
            origemData: OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static FaseCronograma FaseConforme() =>
        FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", origemData: OrigemDataFase.NoEdital,
            agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static FaseCronograma FaseDoResultadoDaClassificacao(int ordem, string codigo) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    // Publicar sem declarar a cobrança de taxa é recusado; declarada, ela isola a pendência que o teste observa.
    private static ConfiguracaoTaxaInscricao TaxaSemCobranca() =>
        ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!;

    private static EtapaProcesso EtapaDaProvaObjetivaNoResultadoFinal() =>
        EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 3m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_FINAL").Value!;
}
