namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Verifica a persistência das regras de comprovação e seus itens no banco de dados.</summary>
public sealed class RegraDeComprovacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public RegraDeComprovacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static DocumentoExigido Documento(Guid faseId, string chave) => DocumentoExigido.Criar(
        Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar(chave).Value!,
        new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
            new MomentoDaExigencia(faseId, null, null),
            new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
            new IndeferimentoDoDocumento(true, null, []),
            RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
            null)).Value!;

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "Persiste e recarrega grupos de quantidade mínima e de todos os itens em outro contexto")]
    public async Task PersisteERecarrega_RegrasComGrupos_EmOutroContexto()
    {
        ProcessoSeletivo processo = ProcessoComGruposDeRegras();
        await PersistirAsync(processo);

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await ProcessoSeletivoRepositoryDeTeste.Criar(readContext)
            .ObterParaMutacaoAsync(processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        AfirmarRegrasRecarregadas(recarregado!);
        AfirmarSemCausasDasRegras(recarregado!);
    }

    private static ProcessoSeletivo ProcessoComGruposDeRegras()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("Processo com grupos de regras", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseDeInscricao();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        Result definirResult = processo.DefinirDocumentosExigidos([RegraSemGrupoComQuantidadeMinimaETodosOsItens(fase.Id)]);
        definirResult.IsSuccess.Should().BeTrue(definirResult.Error?.Message);

        return processo;
    }

    private static RegraDeComprovacao RegraSemGrupoComQuantidadeMinimaETodosOsItens(Guid faseId)
    {
        RegraDeComprovacao grupoDeTodosOsItens = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId, "exigencia_identidade_1"), 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId, "exigencia_identidade_2"), 1).Value!]).Value!;
        BaseLegalDaComprovacao baseLegalDoGrupo = BaseLegalDaComprovacao.Criar(
            "Res. Unifesspa 532/2021", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

        return RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, "ELIMINA", [baseLegalDoGrupo], [grupoDeTodosOsItens, RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId, "exigencia_identidade_3"), 1).Value!]).Value!;
    }

    private async Task PersistirAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext writeContext = _fixture.CreateDbContext();
        await ProcessoSeletivoRepositoryDeTeste.Criar(writeContext).AdicionarAsync(processo, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }

    private static void AfirmarRegrasRecarregadas(ProcessoSeletivo recarregado)
    {
        recarregado.RegrasDeComprovacao.Should().HaveCount(5, "dois grupos e três regras de documento");

        RegraDeComprovacao regraSemGrupoRecarregada = recarregado.RegrasDeComprovacaoSemGrupo.Should().ContainSingle().Which;
        regraSemGrupoRecarregada.Forma.Should().Be(FormaDeComprovacao.QuantidadeMinimaDeItens);
        regraSemGrupoRecarregada.Consequencia.Should().Be("ELIMINA");
        regraSemGrupoRecarregada.BasesLegais.Should().ContainSingle().Which.Situacao.Should().Be(StatusBaseLegal.Resolvido);
        regraSemGrupoRecarregada.Itens.Should().HaveCount(2, "grupo de todos os itens e regra de documento");

        RegraDeComprovacao grupoDeTodosOsItensRecarregado = regraSemGrupoRecarregada.Itens.Should().ContainSingle(n => n.Forma == FormaDeComprovacao.TodosOsItens).Which;
        grupoDeTodosOsItensRecarregado.Itens.Should().HaveCount(2);
        grupoDeTodosOsItensRecarregado.Itens.Should().OnlyContain(f => f.Forma == FormaDeComprovacao.Documento && f.DocumentoExigido != null,
            "cada regra de documento mantém seu documento exigido");
    }

    // O processo cru deixa pendente o resto da configuração; aqui só importam as causas do bloco das exigências documentais.
    private static void AfirmarSemCausasDasRegras(ProcessoSeletivo processo)
    {
        Result<ConfiguracaoAptaACongelar> conferida = ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo));
        FieldError[] dasRegras = [.. conferida.Errors.Where(static e => e.Field == "documentosExigidos" && e.Origem is null)];
        dasRegras.Should().BeEmpty(string.Join(";", dasRegras.Select(static e => e.Error.Message)));
    }

    [Fact(DisplayName = "Substituir regras com grupos repetidamente na mesma sessão preserva a integridade")]
    public async Task SubstituiRegrasComGrupo_MultiplasVezesSeguidas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("Processo com substituição de regras", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseDeInscricao();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        await using UniPlusDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        await SubstituirRegrasNaMesmaSessaoAsync(context, processo, fase.Id);

        processo.RegrasDeComprovacaoSemGrupo.Should().ContainSingle().Which.Forma.Should().Be(FormaDeComprovacao.TodosOsItens);
    }

    // As restrições são externas ao modelo e precisam ser deferíveis para permitir substituições na mesma transação.
    [Theory(DisplayName = "As exclusion constraints de ordem existem no catálogo, como EXCLUDE deferível")]
    [InlineData("ex_regras_comprovacao_sem_grupo_ordem")]
    [InlineData("ex_regras_comprovacao_itens_ordem")]
    public async Task ExclusionConstraintsDeOrdem_ExistemNoCatalogo_ComoDeferiveis(string nomeDaConstraint)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        await context.Database.OpenConnectionAsync();

        await using NpgsqlCommand command = new(
ConsultaDasRestricoesDeOrdem,
            (NpgsqlConnection)context.Database.GetDbConnection());
        command.Parameters.AddWithValue("nome", nomeDaConstraint);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue($"a constraint '{nomeDaConstraint}' deveria existir no catálogo");

        reader.GetString(0).Should().Be("x", "'x' é o contype de EXCLUDE no pg_constraint");
        reader.GetBoolean(1).Should().BeTrue("a checagem deferível evita colisões transitórias durante a substituição");
        reader.GetBoolean(2).Should().BeTrue("INITIALLY DEFERRED — checada só no COMMIT, não a cada statement");
    }

    private const string ConsultaDasRestricoesDeOrdem =
            """
            SELECT contype::text, condeferrable, condeferred
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'selecao'
              AND t.relname = 'regras_de_comprovacao'
              AND c.conname = @nome
            """;


    private static async Task SubstituirRegrasNaMesmaSessaoAsync(UniPlusDbContext context, ProcessoSeletivo processo, Guid faseId)
    {
        for (int tentativa = 1; tentativa <= 3; tentativa++)
        {
            RegraDeComprovacao grupoDeTodosOsItens = GrupoComDoisDocumentos(faseId, tentativa);

            Result definirResult = processo.DefinirDocumentosExigidos([grupoDeTodosOsItens]);
            definirResult.IsSuccess.Should().BeTrue(definirResult.Error?.Message);

            Func<Task> salvar = async () => await context.SaveChangesAsync(CancellationToken.None);
            await salvar.Should().NotThrowAsync(
                $"tentativa {tentativa}: substituir regras com grupo por outras não pode falhar com " +
                "violação transitória de índice único");
        }

    }


    private static RegraDeComprovacao GrupoComDoisDocumentos(Guid faseId, int tentativa)
    {
        RegraDeComprovacao grupoDeTodosOsItens = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId, $"exigencia_identidade_{tentativa}_1"), 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId, $"exigencia_identidade_{tentativa}_2"), 1).Value!]).Value!;

        return grupoDeTodosOsItens;
    }

}
