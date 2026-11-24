namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem.PesoAreaEnemTestHelper;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class PesoAreaEnemRestricoesTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public PesoAreaEnemRestricoesTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "UNIQUE parcial (resolução, grupo) rejeita segundo par vivo idêntico")]
    public async Task IndiceUnicoParcial_Par_RejeitaDuplicataAtiva()
    {
        string resolucao = ResolucaoUnica();
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminA))
        {
            contexto.PesosAreaEnem.Add(Nova(resolucao));
            await contexto.SaveChangesAsync();
        }

        await using UniPlusDbContext contextoDuplicata = _fixture.CreateDbContext(AdminA);
        contextoDuplicata.PesosAreaEnem.Add(Nova(resolucao));

        Func<Task> act = async () => await contextoDuplicata.SaveChangesAsync();

        await ConferirViolacaoDoIndiceUnicoAsync(act);
    }

    [Fact(DisplayName = "Mesma resolução em grupo distinto é aceita (par distinto)")]
    public async Task ParDistinto_MesmaResolucaoOutroGrupo_Aceita()
    {
        string resolucao = ResolucaoUnica();
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(AdminA);
        contexto.PesosAreaEnem.Add(Nova(resolucao, GrupoCurso.Tecnologica));
        contexto.PesosAreaEnem.Add(Nova(resolucao, GrupoCurso.HumanisticaI));

        Func<Task> act = async () => await contexto.SaveChangesAsync();
        await act.Should().NotThrowAsync("o par (resolução, grupo) é distinto");
    }

    [Fact(DisplayName = "Remoção lógica preserva a trilha e permite reutilizar a combinação do par")]
    public async Task RemocaoLogica_PreservaTrilhaELiberaPar()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem peso = Nova(resolucao);
        await GravarPesoAsync(_fixture, peso);

        await RemoverPesoAsync(_fixture, peso.Id);

        await ConferirPesoRemovidoAsync(peso);

        await using UniPlusDbContext contextoNovo = _fixture.CreateDbContext(AdminA);
        contextoNovo.PesosAreaEnem.Add(Nova(resolucao));

        Func<Task> act = async () => await contextoNovo.SaveChangesAsync();
        await act.Should().NotThrowAsync("a remoção lógica permite reutilizar a combinação do par");
    }

    [Theory(DisplayName = "CHECK de banco rejeita grupo fora dos quatro códigos via SQL cru, inclusive o rótulo")]
    [InlineData("Engenharias")]
    [InlineData("Tecnológica")]
    public async Task Check_RejeitaGrupoForaDoDominioViaSqlCru(string grupo)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await contexto.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.pesos_por_area_do_enem (id, resolucao, grupo_curso, grupo_curso_rotulo, base_legal, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {ResolucaoUnica()}, {grupo}, {"Rótulo"}, {BaseLegal}, {DateTimeOffset.UtcNow}, {false})");

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.ConstraintName.Should().Be("ck_pesos_por_area_do_enem_grupo_curso");
    }

    [Theory(DisplayName = "CHECKs da tabela de áreas rejeitam código fora das cinco, peso negativo e corte fora da faixa via SQL cru")]
    [InlineData("FISICA", 1.0, null, "ck_areas_dos_pesos_do_enem_codigo")]
    [InlineData("REDACAO", -1.0, null, "ck_areas_dos_pesos_do_enem_peso")]
    [InlineData("REDACAO", 1.0, 1000.001, "ck_areas_dos_pesos_do_enem_corte")]
    [InlineData("REDACAO", 1.0, -0.5, "ck_areas_dos_pesos_do_enem_corte")]
    public async Task Check_TabelaDeAreas_RejeitaForaDaFaixa(string codigo, double peso, double? corte, string constraint)
    {
        Guid paiId = await CriarPaiSemAreasAsync();
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        decimal? corteDecimal = corte is null ? null : (decimal)corte.Value;

        Func<Task> act = async () => await contexto.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.areas_dos_pesos_do_enem (peso_area_enem_id, codigo, rotulo, peso, corte) VALUES ({paiId}, {codigo}, {"Rótulo"}, {(decimal)peso}, {corteDecimal})");

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.ConstraintName.Should().Be(constraint);
    }

    [Fact(DisplayName = "A chave (linha, código) rejeita a mesma área duas vezes na mesma linha via SQL cru")]
    public async Task Pk_RejeitaAreaRepetidaNaLinha()
    {
        Guid paiId = await CriarPaiSemAreasAsync();
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        await contexto.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.areas_dos_pesos_do_enem (peso_area_enem_id, codigo, rotulo, peso) VALUES ({paiId}, {"REDACAO"}, {"Redação"}, {1.0m})");

        Func<Task> act = async () => await contexto.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.areas_dos_pesos_do_enem (peso_area_enem_id, codigo, rotulo, peso) VALUES ({paiId}, {"REDACAO"}, {"Redação"}, {2.0m})");

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be("23505");
    }

    [Theory(DisplayName = "Corte da Redação nos limites e sem corte persistem como gravados")]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(1000.0)]
    public async Task CorteDaRedacao_PersisteComoGravado(double? corte)
    {
        decimal? esperado = corte is null ? null : (decimal)corte.Value;
        List<AreaInformada> areas = Areas();
        areas[0] = new(PesoAreaEnem.CodigoRedacao, 2.00m, esperado);
        PesoAreaEnem peso = PesoAreaEnem.Criar(ResolucaoUnica(), GrupoCurso.Tecnologica, areas, BaseLegal).Value!;

        await GravarPesoAsync(_fixture, peso);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await contextoLeitura.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.AreasDaLinha[0].Corte.Should().Be(esperado);
    }

    // Linha de pesos sem áreas, direto no banco: base para exercitar os CHECKs da tabela
    // filha sem a validação do agregado no caminho. Nasce removida logicamente — o banco é
    // compartilhado pela coleção, e uma linha viva incompleta apareceria para os testes
    // que listam ou editam os pesos vivos.
    private async Task<Guid> CriarPaiSemAreasAsync()
    {
        Guid id = Guid.CreateVersion7();
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        await contexto.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.pesos_por_area_do_enem (id, resolucao, grupo_curso, grupo_curso_rotulo, base_legal, created_at, is_deleted, deleted_at) VALUES ({id}, {ResolucaoUnica()}, {GrupoCurso.Tecnologica}, {"Tecnológica"}, {BaseLegal}, {agora}, {true}, {agora})");
        return id;
    }

    private static async Task ConferirViolacaoDoIndiceUnicoAsync(Func<Task> act)
    {
        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsParConflict) em
        // ParJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.PesoPorAreaDoEnemResolucaoGrupoAtivos);
    }

    private async Task ConferirPesoRemovidoAsync(PesoAreaEnem peso)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null))
        {
            PesoAreaEnem excluida = await contexto.PesosAreaEnem
                .IgnoreQueryFilters().SingleAsync(p => p.Id == peso.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
            excluida.AreasDaLinha.Should().HaveCount(5, "a remoção lógica preserva as áreas junto com a linha");
            (await ContarAreasAsync(contexto, peso.Id)).Should().Be(5);
        }
    }
}
