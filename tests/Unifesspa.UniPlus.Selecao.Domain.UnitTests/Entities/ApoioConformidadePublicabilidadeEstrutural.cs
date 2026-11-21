namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioConformidadePublicabilidadeEstrutural
{
    /// <summary>Só os itens de <see cref="ProcessoSeletivo.AvaliarConformidade"/> que se espera ver vermelhos.</summary>
    internal static void SoEstesItensVermelhos(ProcessoSeletivo processo, params string[] itensVermelhos) =>
        SoEstesItensVermelhos(processo, ContagemDePrazosDeTeste.ComCalendarioVigente, itensVermelhos);

    internal static void SoEstesItensVermelhos(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto, params string[] itensVermelhos) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(contexto, motivosEscolhidos: MotivosDoProcessoDeTeste.Cobrindo(processo)).ComRegrasDo(processo)).Where(static i => !i.Ok).Select(static i => i.Codigo)
            .Should().BeEquivalentTo(itensVermelhos);

    internal static ItemConformidade Item(ProcessoSeletivo processo, string codigo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == codigo);

    internal static EtapaProcesso EtapaDeProvaObjetiva(
        string nome, CaraterEtapa carater, int? ordem, decimal? peso, decimal? notaMinima, string? faseCodigo) =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: notaMinima, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    internal static FatoColetado FatoDeOpcaoDeTeste() => FatoColetado.Criar(
        "FATO_OPCAO_DE_TESTE", 0, "Fato de opção do processo", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
        origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    internal static OpcaoDeclaradaFato Opcao(string codigo, int ordem = 0) =>
        OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", codigo, $"Opção {codigo}", ordem).Value!;

    internal static AtendimentoEspecializado OfertaComCondicaoPcd() => AtendimentoEspecializado.Criar(
        [CondicaoOfertada.Criar(Guid.CreateVersion7(), AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
        [],
        []).Value!;

    internal static readonly string HashFixo = new('a', 64);

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    internal static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    internal static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) =>
        Publicar(processo, ContagemDePrazosDeTeste.ComCalendarioVigente);

    internal static Result<VersaoConfiguracao> Publicar(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto) => processo.Publicar(
            new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "teste", Contagem = contexto });

    internal static ConfiguracaoClassificacao ClassificacaoImportada() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
        RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
    }).Value!;

    internal static ModalidadeSelecionada Modalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        RegraRemanejamentoModalidade remanejamento,
        int? quantidadeDeclarada,
        string? acaoQuandoIndeferido = null,
        PredicadoDnf? regra = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.RestanteDasVagasOfertadas : ComposicaoVagasModalidade.DentroDaReservaLegal,
            null, remanejamento, null, null, null, regra ?? RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza), acaoQuandoIndeferido, "base legal", quantidadeDeclarada).Value!;

    internal static readonly (string? Codigo, string? Rotulo) GrupoTecnologica = ("TECNOLOGICA", "Tecnológica");

    internal static ConfiguracaoDistribuicaoVagas DistribuicaoAmpla(int vagasOfertadasBase, string? acaoQuandoIndeferido = null) =>
        OfertaAmpla(vagasOfertadasBase, grupoAreaEnem: null, acaoQuandoIndeferido);

    internal static ConfiguracaoDistribuicaoVagas OfertaAmpla(
        int vagasOfertadasBase, (string? Codigo, string? Rotulo)? grupoAreaEnem, string? acaoQuandoIndeferido = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = vagasOfertadasBase, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, vagasOfertadasBase, acaoQuandoIndeferido)] },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem, OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    /// <summary>
    /// Os itens vermelhos do checklist, sem os do formulário: os testes que isolam uma razão de
    /// recusa montam processos sem formulário, ou com o formulário mínimo sem a fase de inscrição, e
    /// os itens do formulário ficariam vermelhos por isso — legitimamente, mas fora do que o teste
    /// isola.
    /// </summary>
    internal static IEnumerable<ItemConformidade> VermelhosForaDoFormulario(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Where(static i => !i.Ok && !i.Codigo.StartsWith("formulario_", StringComparison.Ordinal));

    /// <summary>Fase mínima e coerente: não agrupa etapas (dispensável sem prova), produz resultado, não coleta inscrição.</summary>
    internal static FaseCronograma FaseBase(
        bool coletaInscricao = false, bool coletaSolicitacaoIsencao = false, Guid? faseCanonicaOrigemId = null,
        bool semUnidadeResponsavel = false) => FaseQueHomologa(
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(), [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()], coletaInscricao, coletaSolicitacaoIsencao, faseCanonicaOrigemId, semUnidadeResponsavel);

    /// <summary>A fase que homologa as inscrições de quem importa os candidatos: com o resultado preliminar, a janela de recurso e o definitivo.</summary>
    internal static FaseCronograma FaseBaseDaImportacao(Guid? faseCanonicaOrigemId = null, IReadOnlyList<BancaRequerida>? bancas = null) => FaseQueHomologa(
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologaComRecurso(), bancas ?? HomologacaoDasInscricoesDeTeste.BancasDaFaseQueHomologaComRecurso(), faseCanonicaOrigemId: faseCanonicaOrigemId);

    private static FaseCronograma FaseQueHomologa(
        PublicacoesDaFase publicacoes, IReadOnlyList<BancaRequerida> bancas, bool coletaInscricao = false, bool coletaSolicitacaoIsencao = false,
        Guid? faseCanonicaOrigemId = null, bool semUnidadeResponsavel = false) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, coletaInscricao, coletaSolicitacaoIsencao),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: semUnidadeResponsavel ? null : UnidadesDeTeste.Ceps),
        publicacoes,
        bancas).Value!;

    /// <summary>
    /// Processo estruturalmente conforme às QUATRO conferências estruturais: sem etapa (ImportacaoExterna +
    /// ClassificacaoImportada dispensam prova), atendimento, distribuição, classificação e
    /// cronograma coerente com uma única fase que produz resultado. Sem cascata (nenhuma
    /// modalidade SegueCascata) e sem exigências/regras de derivação/fatos coletados — as quatro
    /// conferências ficam vazias/Ok até que o teste acrescente a única razão que quer isolar. A taxa vem declarada, porque publicar sem
    /// declará-la é recusado, e a convenção de contagem também, porque a janela de recurso da importação a exige.
    /// </summary>
    internal static ProcessoSeletivo ProcessoConforme(IReadOnlyList<ConfiguracaoDistribuicaoVagas>? distribuicao = null)
    {
        ProcessoSeletivo processo = ProcessoVazioDaMatrizEstrutural();

        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(distribuicao ?? [DistribuicaoAmpla(10)], FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada()).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseBaseDaImportacao()], []).IsSuccess.Should().BeTrue();
        DeclararTaxaSemCobranca(processo);
        ContagemDePrazosDeTeste.DeclararConvencao(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    private static ProcessoSeletivo ProcessoVazioDaMatrizEstrutural() => ProcessoSeletivo.Criar(
        "PS Matriz Estrutural", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    internal static IReadOnlyList<ItemConformidade> Checklist(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo));

    /// <summary>Uma única pendência vermelha no checklist, a dada, e a publicação recusada com o código dado.</summary>
    internal static void AfirmarPendenciaUnicaERecusa(ProcessoSeletivo processo, string itemVermelho, string codigoDaRecusa)
    {
        Checklist(processo).Should().ContainSingle(i => !i.Ok).Which.Codigo.Should().Be(itemVermelho);
        ExigirPublicacaoRecusada(processo, codigoDaRecusa);
    }

    internal static void ExigirPublicacaoRecusada(ProcessoSeletivo processo, string codigoDaRecusa)
    {
        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoDaRecusa);
    }

    internal static void DeclararTaxaSemCobranca(ProcessoSeletivo processo)
    {
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }
}
