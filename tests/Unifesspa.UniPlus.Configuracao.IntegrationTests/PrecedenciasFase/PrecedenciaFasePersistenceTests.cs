namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PrecedenciasFase;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Integração ponta-a-ponta de <c>PrecedenciaFase</c> contra Postgres real
/// (UNI-REQ-0064, story #851): seed das quatro arestas estruturais (CA-05), leitura
/// via <see cref="IPrecedenciaFaseReader"/>, UNIQUE parcial do par vivo e CHECKs de
/// domínio (formato, conjunto canônico, self-loop).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PrecedenciaFasePersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly (string Antecessora, string Sucessora)[] ArestasEsperadas =
    [
        ("INSCRICAO", "HOMOLOGACAO_INSCRICOES"),
        ("RESULTADO_FINAL", "HABILITACAO"),
        ("HABILITACAO", "MATRICULA"),
        ("HETEROIDENTIFICACAO", "HOMOLOGACAO_RESULTADO_FINAL"),
    ];

    private readonly ConfiguracaoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDePrecedenciaFase _apoio;

    public PrecedenciaFasePersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDePrecedenciaFase(fixture);
    }

    [Fact(DisplayName = "CA-05: o seed materializa as quatro arestas canônicas e o reader devolve o grafo")]
    public async Task Seed_MaterializaArestasCanonicas()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new PrecedenciaFaseReader(ctx);

        IReadOnlyList<PrecedenciaFaseView> grafo = await reader.ListarVivasAsync();

        foreach ((string antecessora, string sucessora) in ArestasEsperadas)
        {
            grafo.Should().Contain(
                a => a.AntecessoraCodigo == antecessora && a.SucessoraCodigo == sucessora,
                $"a aresta {antecessora}→{sucessora} deve estar semeada");
        }
    }

    [Fact(DisplayName = "Criar persiste a aresta e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        PrecedenciaFase aresta = Aresta("ENSALAMENTO", "AVALIACAO");

        PrecedenciaFase persistida = await _apoio.GravarELerDeNovoAsync(AdminA, aresta);

        persistida.AntecessoraCodigo.Should().Be("ENSALAMENTO");
        persistida.SucessoraCodigo.Should().Be("AVALIACAO");
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();

        PrecedenciaFaseView? view = await _apoio.ObterPeloLeitorAsync(aresta.Id);
        view.Should().NotBeNull();
        view!.AntecessoraCodigo.Should().Be("ENSALAMENTO");
        view.SucessoraCodigo.Should().Be("AVALIACAO");
    }

    [Fact(DisplayName = "UNIQUE parcial do par rejeita segunda aresta viva com o mesmo par")]
    public async Task UniquePartial_Par_RejeitaDuplicataAtiva()
    {
        await _apoio.GravarAsync(AdminA, Aresta("CLASSIFICACAO", "CHAMADA"));

        Func<Task> act = () => _apoio.GravarAsync(AdminA, Aresta("CLASSIFICACAO", "CHAMADA"));

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.PrecedenciaEntreFasesParVivo);
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do par")]
    public async Task SoftDelete_LiberaSlot()
    {
        PrecedenciaFase aresta = Aresta("LISTA_ESPERA", "CHAMADA");
        await _apoio.GravarAsync(AdminA, aresta);
        await _apoio.ExcluirAsync(AdminB, aresta.Id);

        PrecedenciaFase excluida = await _apoio.LerExcluidaAsync(aresta.Id);
        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedBy.Should().Be(AdminB);

        Func<Task> act = () => _apoio.GravarAsync(AdminA, Aresta("LISTA_ESPERA", "CHAMADA"));
        await act.Should().NotThrowAsync("o slot do par foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita self-loop via SQL cru")]
    public async Task Check_RejeitaSelfLoopViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.precedencias_entre_fases (id, antecessora_codigo, sucessora_codigo, permite_sobreposicao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"INSCRICAO"}, {"INSCRICAO"}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK antecessora_codigo <> sucessora_codigo impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código de antecessora fora do conjunto canônico via SQL cru")]
    public async Task Check_RejeitaAntecessoraForaDoCanonicoViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.precedencias_entre_fases (id, antecessora_codigo, sucessora_codigo, permite_sobreposicao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"FASE_INVALIDA"}, {"HOMOLOGACAO_INSCRICOES"}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de conjunto canônico impede o INSERT direto");
    }

    // As duas CHECKs de precedencias_entre_fases repetem o rol separadamente. Exercitar os
    // dois sentidos é o que distingue "acrescentei o código nas três" de "acrescentei
    // só na de tipos_de_fase" — neste último caso a fase existiria no cadastro e
    // nenhuma aresta poderia referenciá-la.
    [Fact(DisplayName = "CHECK de banco aceita a solicitação de isenção como antecessora e como sucessora")]
    public async Task Check_AceitaSolicitacaoIsencaoNosDoisSentidos()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> comoSucessora = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.precedencias_entre_fases (id, antecessora_codigo, sucessora_codigo, permite_sobreposicao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"INSCRICAO"}, {FaseCanonicaCatalogo.CodigoSolicitacaoIsencao}, {true}, {DateTimeOffset.UtcNow}, {false})");

        Func<Task> comoAntecessora = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.precedencias_entre_fases (id, antecessora_codigo, sucessora_codigo, permite_sobreposicao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {FaseCanonicaCatalogo.CodigoSolicitacaoIsencao}, {"HOMOLOGACAO_INSCRICOES"}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await comoSucessora.Should().NotThrowAsync(
            "a migration acrescentou o código ao CHECK da sucessora");
        await comoAntecessora.Should().NotThrowAsync(
            "a migration acrescentou o código ao CHECK da antecessora");
    }

    private static PrecedenciaFase Aresta(string antecessora, string sucessora) =>
        PrecedenciaFase.Criar(antecessora, sucessora, false, []).Value!;
}
