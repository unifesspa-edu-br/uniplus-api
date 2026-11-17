namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A coincidência entre a conferência de publicação e o item de conformidade da conclusão do ciclo
/// recursal. <c>DefinirCronogramaFases</c> recusa a fase que publica preliminar sem conclusão
/// alcançável, mas esse estado é <b>materializável</b>: o EF hidrata os produtos direto das
/// linhas, sem passar pela fábrica, e uma configuração gravada antes da regra volta ao agregado
/// nesse estado.
/// </summary>
/// <remarks>
/// A prova precisa de Postgres real e de SQL cru: nenhum caminho do domínio produz o estado que
/// se está defendendo. Semear pelo agregado e adulterar a linha depois é o que reproduz a
/// hidratação de dado legado — mesmo raciocínio de
/// <see cref="TaxaInscricaoSemFundamentoPersistenciaTests"/>.
/// </remarks>
public sealed class ConclusaoDoCicloRecursalPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("cf01234567", 7))[..64];

    private readonly ProcessoSeletivoDbFixture _fixture;

    public ConclusaoDoCicloRecursalPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    // O único produto da fase deixa de ser a definitiva e passa a ser preliminar: a fase abre um ciclo recursal que nada conclui,
    // estado que a fábrica recusa produzir hoje.
    [Fact(DisplayName = "Fase hidratada publicando só preliminar, sem conclusão declarada, marca o item vermelho e recusa a publicação")]
    public async Task PreliminarSemConclusao_ItemVermelhoEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1426 {Guid.CreateVersion7()}");
        await RebaixarProdutoParaPreliminarAsync(processoId);
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.CronogramaFases.Should().ContainSingle()
            .Which.Produtos.Should().ContainSingle()
            .Which.Papel.Should().Be(PapelProdutoFase.Preliminar);
        AfirmarConclusaoVermelhaEPublicacaoRecusada(processo);
    }

    private static void AfirmarConclusaoVermelhaEPublicacaoRecusada(ProcessoSeletivo processo)
    {
        IReadOnlyList<ItemConformidade> checklist = ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Padrao);

        checklist.Should().ContainSingle(i => i.Codigo == "cronograma_conclusao_do_ciclo_recursal")
            .Which.Ok.Should().BeFalse(
                "quem monta o edital precisa ver a pendência; invariante que só existe no domínio não aparece no checklist");
        checklist.Should().ContainSingle(i => i.Codigo == "cronograma_vagas_sem_fase_que_produz_resultado")
            .Which.Ok.Should().BeTrue("a fase continua produzindo resultado — o que falta é a conclusão da matéria");

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsFailure.Should().BeTrue(
            "linha legada não pode congelar uma versão com o ciclo recursal em aberto");
        publicacao.Error!.Code.Should().Be("ProcessoSeletivo.ConclusaoNaoDeclarada",
            "PendenciaDoCronograma precede o agregador genérico, e a causa nomeada é a que orienta");
    }

    [Fact(DisplayName = "Contraprova: a mesma configuração com a definitiva publicada tem o item verde e publica")]
    public async Task DefinitivaPublicada_ItemVerdeEPublica()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1426 ok {Guid.CreateVersion7()}");

        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Padrao)
            .Should().ContainSingle(i => i.Codigo == "cronograma_conclusao_do_ciclo_recursal")
            .Which.Ok.Should().BeTrue();

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
    }

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(new CenarioDeCongelamento
    {
        Dados = DadosEdital.Criar(
            "001/2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            Guid.CreateVersion7()).Value!,
        Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo),
        AtorUsuarioContaId = "teste",
    });

    /// <summary>
    /// Processo estruturalmente publicável cuja fase publica a definitiva da matéria — o único
    /// jeito de chegar à linha, porque a fábrica recusa gravar a preliminar órfã.
    /// </summary>
    private async Task<Guid> SemearProcessoConformeAsync(string nome)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases([FaseConforme()], [])
            .IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
                new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [modalidade] },
                new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
            {
                RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
                RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            }).Value!).IsSuccess.Should().BeTrue();

        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        return processo.Id;
    }

    private async Task RebaixarProdutoParaPreliminarAsync(Guid processoId)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(
            """
            UPDATE selecao.produtos_da_fase p
               SET papel = @preliminar
              FROM selecao.fases_cronograma f
             WHERE p.fase_cronograma_id = f.id
               AND f.processo_seletivo_id = @processo
            """,
            conexao);
        comando.Parameters.AddWithValue("preliminar", (int)PapelProdutoFase.Preliminar);
        comando.Parameters.AddWithValue("processo", processoId);

        int afetadas = await comando.ExecuteNonQueryAsync();
        afetadas.Should().Be(1, "a semeadura grava exatamente um produto para a única fase do processo");
    }

    private static async Task<ProcessoSeletivo> CarregarAsync(UniPlusDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.Formularios).ThenInclude(f => f.Etapas)
            .Include(p => p.Campos).ThenInclude(f => f.Precondicoes)
            .Include(p => p.AtendimentoEspecializado)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .Include(p => p.CriteriosDesempate)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;
}
