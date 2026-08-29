namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Prova que a migration que corrige a base legal de <c>AC_PCD</c> alcança a linha semeada e
/// <b>não</b> alcança uma que um admin já tenha reescrito.
/// </summary>
/// <remarks>
/// <c>base_legal</c> é uma das duas colunas (com <c>descricao</c>) que o cadastro deixa editar
/// numa modalidade do catálogo legal fixo — <c>Modalidade.Atualizar</c> aceita as duas e recusa
/// o resto. Uma correção de seed por <c>UpdateData</c> em cima do id descartaria essa edição em
/// silêncio, e ainda deixaria <c>updated_by</c>/<c>updated_at</c> descrevendo um valor que não
/// existe mais. Por isso o <c>UPDATE</c> é condicionado ao texto semeado, e é isso que este
/// teste fixa.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — não recebe entrada externa.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class CorrecaoBaseLegalAcPcdTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260829005332_SemeiaModalidadesInstitucionaisPsiq";

    private const string IdAcPcd = "70da1000-0000-7000-8000-000000000010";
    private const string IdPcdPuro = "70da1000-0000-7000-8000-000000000011";

    private const string BaseLegalInstitucional =
        "Res. Unifesspa 532/2021, art. 1º (reserva de vaga para pessoa com deficiência)";

    private const string BaseLegalEditadaPeloAdmin =
        "Res. Unifesspa 532/2021, art. 1º; Portaria MEC 18/2012, art. 12, II — redação do CEPS";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_correcao_base_legal_ac_pcd_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "A migration corrige a base legal semeada de AC_PCD e preserva a que um admin reescreveu")]
    public async Task Migration_CorrigeSemeadaEPreservaEdicaoAdministrativa()
    {
        // Estado do mundo ANTES desta correção: AC_PCD com a base legal da Lei de Cotas, e
        // PCD_PURO — semeada com o valor institucional — reescrita por um admin pelo caminho
        // suportado (PUT admin/modalidades/{id}, que aceita descrição e base legal). As duas
        // linhas existem desde a migration anterior; aqui só se altera o texto.
        await using (ConfiguracaoDbContext contextoLegado = CriarContexto())
        {
            IMigrator migrator = contextoLegado.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
            await ReescreverBaseLegalComoAdminAsync(IdPcdPuro, BaseLegalEditadaPeloAdmin);
        }

        await using (ConfiguracaoDbContext contextoNovo = CriarContexto())
        {
            await contextoNovo.Database.MigrateAsync();
        }

        await using ConfiguracaoDbContext leitura = CriarContexto();

        Modalidade acPcd = await leitura.Modalidades.AsNoTracking()
            .SingleAsync(m => m.Id == new Guid(IdAcPcd), CancellationToken.None);
        acPcd.BaseLegal.Should().Be(BaseLegalInstitucional,
            "a linha ainda carregava o texto do seed — é exatamente a que a correção existe para alcançar");

        Modalidade pcdPuro = await leitura.Modalidades.AsNoTracking()
            .SingleAsync(m => m.Id == new Guid(IdPcdPuro), CancellationToken.None);
        pcdPuro.BaseLegal.Should().Be(BaseLegalEditadaPeloAdmin,
            "base legal é editável por cadastro nas modalidades do catálogo legal fixo — corrigir o "
            + "texto do seed não pode descartar em silêncio o que um admin escreveu");
    }

    private ConfiguracaoDbContext CriarContexto()
    {
        DbContextOptions<ConfiguracaoDbContext> options = new DbContextOptionsBuilder<ConfiguracaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ConfiguracaoDbContext(options);
    }

    /// <summary>
    /// Reescreve <c>base_legal</c> em SQL cru, como o cadastro faria — incluindo a auditoria de
    /// atualização, que é o que tornaria a sobrescrita silenciosa especialmente enganosa.
    /// </summary>
    private async Task ReescreverBaseLegalComoAdminAsync(string id, string baseLegal)
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = """
            UPDATE configuracao.modalidade
               SET base_legal = @baseLegal,
                   updated_at = now(),
                   updated_by = 'admin-ceps'
             WHERE id = @id::uuid;
            """;
        comando.Parameters.AddWithValue("baseLegal", baseLegal);
        comando.Parameters.AddWithValue("id", id);
        await comando.ExecuteNonQueryAsync();
    }
}
