namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A bicondicional entre o gate de publicação e o item de conformidade da âncora do prazo de
/// recurso. <c>FaseCronograma.Criar</c> recusa a regra que ancora fora dos produtos
/// preliminares da própria fase, mas esse estado é <b>materializável</b>: o EF hidrata
/// <c>regras_recurso_fase</c> e <c>produtos_da_fase</c> direto das linhas, sem passar pela
/// fábrica, e <c>produto_ancora_id</c> não tem chave estrangeira nem CHECK que o prenda.
/// </summary>
/// <remarks>
/// <para>
/// Sem as duas metades, o codificador produz um estado que o próprio decodificador rejeita: a
/// publicação congelaria uma versão cuja âncora não resolve, e a incoerência só apareceria na
/// restauração — longe da causa, e sem o operador ter visto pendência nenhuma no checklist.
/// </para>
/// <para>
/// A prova precisa de Postgres real e de SQL cru: nenhum caminho do domínio produz o estado que
/// se está defendendo. Semear pelo agregado e adulterar a linha depois é o que reproduz a
/// hidratação de dado legado — mesmo raciocínio de
/// <see cref="ConclusaoDoCicloRecursalPersistenciaTests"/>.
/// </para>
/// </remarks>
public sealed class AncoraDoRecursoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("a712345678", 7))[..64];

    private readonly ProcessoSeletivoDbFixture _fixture;

    public AncoraDoRecursoPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Âncora hidratada sobre produto DEFINITIVO marca o item vermelho e recusa a publicação")]
    public async Task AncoraEmProdutoDefinitivo_ItemVermelhoEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1427 ancora {Guid.CreateVersion7()}");

        // A âncora passa a apontar para o produto DEFINITIVO da mesma fase: continua sendo um
        // produto dela, então nenhuma checagem de pertinência sozinha veria o defeito — o que
        // some é a decisão contestável de que o prazo contaria.
        await ApontarAncoraParaAsync(processoId, "RESULTADO_FINAL");

        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        FaseCronograma fase = processo.CronogramaFases.Single();
        fase.RegraRecurso!.ProdutoAncoraId.Should().Be(
            fase.Produtos.Single(p => p.AtoCodigo == "RESULTADO_FINAL").Id,
            "pré-condição: o EF hidratou a âncora incoerente sem passar por fábrica nenhuma");

        IReadOnlyList<ItemConformidade> checklist = processo.AvaliarConformidade(ComCalendario());

        checklist.Should().ContainSingle(i => i.Codigo == "cronograma_ancora_do_recurso")
            .Which.Ok.Should().BeFalse(
                "quem monta o edital precisa ver a pendência; invariante que só existe no domínio não aparece no checklist");
        checklist.Should().ContainSingle(i => i.Codigo == "cronograma_conclusao_do_ciclo_recursal")
            .Which.Ok.Should().BeTrue("a fase continua concluindo o próprio ciclo — o que quebrou foi a âncora");

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsFailure.Should().BeTrue(
            "linha legada não pode congelar uma versão cujo prazo de interposição não resolve para publicação nenhuma");
        publicacao.Error!.Code.Should().Be("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase",
            "PendenciaDoCronograma precede o agregador genérico, e a causa nomeada é a que orienta");
    }

    [Fact(DisplayName = "Âncora hidratada apontando para produto de OUTRA fase marca o item vermelho e recusa a publicação")]
    public async Task AncoraForaDaFase_ItemVermelhoEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1427 ancora fora {Guid.CreateVersion7()}");

        await ApontarAncoraParaProdutoInexistenteAsync(processoId);

        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.AvaliarConformidade(ComCalendario())
            .Should().ContainSingle(i => i.Codigo == "cronograma_ancora_do_recurso")
            .Which.Ok.Should().BeFalse();

        Publicar(processo).IsFailure.Should().BeTrue(
            "sem chave estrangeira, nada no banco impede a linha de apontar para fora da fase — o gate é a defesa");
    }

    [Fact(DisplayName = "Contraprova: a mesma configuração com a âncora no preliminar tem o item verde e publica")]
    public async Task AncoraNoPreliminar_ItemVerdeEPublica()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1427 ancora ok {Guid.CreateVersion7()}");

        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.AvaliarConformidade(ComCalendario())
            .Should().ContainSingle(i => i.Codigo == "cronograma_ancora_do_recurso")
            .Which.Ok.Should().BeTrue();

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
    }

    /// <summary>
    /// Calendário vigente mínimo — o prazo de interposição corre em dia útil, e sem ele a
    /// publicação recusaria por uma pendência que não é a testada aqui.
    /// </summary>
    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        DadosEdital.Criar(
            "001/2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            Guid.CreateVersion7()).Value!,
        "{}"u8.ToArray(),
        "1.1",
        "canonical-json/sha256@v1",
        HashFixo,
        "teste",
        TimeProvider.System,
        ComCalendario());

    /// <summary>
    /// Processo estruturalmente publicável cuja única fase publica o preliminar e o definitivo
    /// da matéria e admite recurso ancorado no preliminar — o único jeito de chegar à linha,
    /// porque a fábrica recusa gravar a âncora incoerente.
    /// </summary>
    private async Task<Guid> SemearProcessoConformeAsync(string nome)
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, notaMinima: null, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
                null, null, [modalidade]).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirAlgoritmoContagemPrazo(
            Regra(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, 'd'), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        return processo.Id;
    }

    /// <summary>Reaponta a âncora para o produto da fase que publica <paramref name="atoCodigo"/>.</summary>
    private Task ApontarAncoraParaAsync(Guid processoId, string atoCodigo) => ExecutarAsync(
        """
        UPDATE selecao.regras_recurso_fase r
           SET produto_ancora_id = p.id
          FROM selecao.fases_cronograma f, selecao.produtos_da_fase p
         WHERE r.fase_cronograma_id = f.id
           AND p.fase_cronograma_id = f.id
           AND p.ato_codigo = @ato
           AND f.processo_seletivo_id = @processo
        """,
        comando =>
        {
            comando.Parameters.AddWithValue("ato", atoCodigo);
            comando.Parameters.AddWithValue("processo", processoId);
        });

    /// <summary>
    /// Reaponta a âncora para um identificador que não é produto de fase nenhuma — a forma
    /// que uma chave estrangeira impediria, e que sem ela nada no banco recusa.
    /// </summary>
    private Task ApontarAncoraParaProdutoInexistenteAsync(Guid processoId) => ExecutarAsync(
        """
        UPDATE selecao.regras_recurso_fase r
           SET produto_ancora_id = @forasteiro
          FROM selecao.fases_cronograma f
         WHERE r.fase_cronograma_id = f.id
           AND f.processo_seletivo_id = @processo
        """,
        comando =>
        {
            comando.Parameters.AddWithValue("forasteiro", Guid.CreateVersion7());
            comando.Parameters.AddWithValue("processo", processoId);
        });

    private async Task ExecutarAsync(string sql, Action<NpgsqlCommand> parametrizar)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(sql, conexao);
        parametrizar(comando);

        int afetadas = await comando.ExecuteNonQueryAsync();
        afetadas.Should().Be(1, "a semeadura grava exatamente uma regra de recurso para a única fase do processo");
    }

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.OfertaAtendimento)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static FaseCronograma FaseConforme()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);

        return FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            coletaInscricao: true,
            coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [preliminar, ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: RegraRecursoFase.Criar(
                Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'e'),
                new ArgsRegraPrazoRecurso(
                    PrazoValor: 48m,
                    PrazoUnidade: UnidadePrazo.Horas,
                    SuspensividadePrimeiraInstanciaValor: null,
                    SuspensividadePrimeiraInstanciaUnidade: null,
                    SuspensividadeSegundaInstanciaValor: null,
                    SuspensividadeSegundaInstanciaUnidade: null),
                preliminar.Id).Value!).Value!;
    }
}
