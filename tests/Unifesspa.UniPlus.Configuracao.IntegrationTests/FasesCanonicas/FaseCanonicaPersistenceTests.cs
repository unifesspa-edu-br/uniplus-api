namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Integração ponta-a-ponta da FaseCanonica contra Postgres real (UNI-REQ-0064):
/// persistência, UNIQUE parcial do código vivo, liberação do slot por soft-delete,
/// CHECKs de domínio (formato, conjunto canônico, coerência de
/// agrupar etapas e complementação) e leitura cross-módulo ordenada por código.
/// Cada teste usa códigos canônicos distintos (o conjunto é fechado).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class FaseCanonicaPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDeFaseCanonica _apoio;

    public FaseCanonicaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDeFaseCanonica(fixture);
    }

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        await _apoio.LiberarSlotDoSeedAsync("INSCRICAO");
        FaseCanonica fase = Fase("INSCRICAO", "Inscrição");

        FaseCanonica persistida = await _apoio.GravarELerDeNovoAsync(AdminA, fase);

        persistida.Codigo.Valor.Should().Be("INSCRICAO");
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();

        FaseCanonicaView? view = await _apoio.ObterPeloLeitorAsync(fase.Id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be("INSCRICAO");
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segunda fase viva com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        await _apoio.LiberarSlotDoSeedAsync("ENSALAMENTO");
        await _apoio.GravarAsync(AdminA, Fase("ENSALAMENTO", "Ensalamento"));

        Func<Task> act = () => _apoio.GravarAsync(AdminA, Fase("ENSALAMENTO", "Ensalamento"));

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.FaseCanonicaCodigoVivo);
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_LiberaSlot()
    {
        await _apoio.LiberarSlotDoSeedAsync("CLASSIFICACAO");
        FaseCanonica fase = Fase("CLASSIFICACAO", "Classificação");
        await _apoio.GravarAsync(AdminA, fase);
        await _apoio.ExcluirAsync(AdminB, fase.Id);

        FaseCanonica excluida = await _apoio.LerExcluidaAsync(fase.Id);
        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedBy.Should().Be(AdminB);

        Func<Task> act = () => _apoio.GravarAsync(AdminA, Fase("CLASSIFICACAO", "Classificação"));
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco aceita a fase de solicitação de isenção")]
    public async Task Check_AceitaSolicitacaoIsencao()
    {
        await _apoio.LiberarSlotDoSeedAsync(FaseCanonicaCatalogo.CodigoSolicitacaoIsencao);

        FaseCanonica fase = Fase(
            FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, "Solicitação de isenção");

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.FasesCanonicas.Add(fase);

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "a migration acrescentou o código ao CHECK de conjunto canônico");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do conjunto canônico via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoCanonicoViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipos_de_fase (id, codigo, nome, agrupa_etapas, permite_complementacao, origem_data, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"FASE_INVALIDA"}, {"Fase"}, {false}, {false}, {"NO_EDITAL"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de conjunto canônico impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do formato via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoFormatoViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipos_de_fase (id, codigo, nome, agrupa_etapas, permite_complementacao, origem_data, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"lista-espera"}, {"Fase"}, {false}, {false}, {"NO_EDITAL"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK codigo ~ '^[A-Z_]+$' impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita agrupar etapas fora da avaliação via SQL cru")]
    public async Task Check_RejeitaAgrupaEtapasForaDaAvaliacaoViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipos_de_fase (id, codigo, nome, agrupa_etapas, permite_complementacao, origem_data, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"HABILITACAO"}, {"Habilitação"}, {true}, {false}, {"NO_EDITAL"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência agrupa_etapas ⇒ avaliação impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita complementação em fase vedada via SQL cru")]
    public async Task Check_RejeitaComplementacaoFaseVedadaViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipos_de_fase (id, codigo, nome, agrupa_etapas, permite_complementacao, origem_data, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"RESULTADO_FINAL"}, {"Resultado final"}, {false}, {true}, {"NO_EDITAL"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência permite_complementacao ⇒ fases permitidas impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita origem da data fora do domínio via SQL cru")]
    public async Task Check_RejeitaOrigemDataForaDoDominioViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipos_de_fase (id, codigo, nome, agrupa_etapas, permite_complementacao, origem_data, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"MATRICULA"}, {"Matrícula"}, {false}, {false}, {"EXTERNA"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de domínio de origem_data impede o INSERT direto");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por código ascendente e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorCodigoEExcluiSoftDeleted()
    {
        await _apoio.LiberarSlotDoSeedAsync("MATRICULA", "HETEROIDENTIFICACAO", "CHAMADA");
        FaseCanonica excluida = Fase("CHAMADA", "Chamada");
        await _apoio.GravarAsync(
            AdminA, Fase("MATRICULA", "Matrícula"), Fase("HETEROIDENTIFICACAO", "Heteroidentificação"), excluida);
        await _apoio.ExcluirAsync(AdminB, excluida.Id);

        IReadOnlyList<FaseCanonicaView> todos = await _apoio.ListarVivasPeloLeitorAsync();

        string[] codigos = [.. todos.Select(v => v.Codigo)];
        codigos.Should().BeInAscendingOrder("o leitor ordena por código ascendente");
        codigos.Should().Contain(["HETEROIDENTIFICACAO", "MATRICULA"]);
        codigos.Should().NotContain("CHAMADA", "a fase soft-deleted não aparece no leitor de vivos");
    }

    private static FaseCanonica Fase(string codigo, string nome) =>
        FaseCanonica.Criar(
            codigo, nome, null, null, "NO_EDITAL",
            // A marca acompanha o código: a factory recusa a fase de isenção sem ela.
            new SinalizadoresDaFase(
                AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: codigo == FaseCanonicaCatalogo.CodigoSolicitacaoIsencao)).Value!;
}
