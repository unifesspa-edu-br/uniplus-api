namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence.Configurations;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

// Valida a junction table de AreaVisibilityConfiguration<TParent> contra
// PostgreSQL real: exclusion constraint GIST de não sobreposição (ADR-0060),
// índice parcial dos vínculos vigentes, FK e ON DELETE RESTRICT. O esquema EF
// vem de EnsureCreatedAsync; o GIST é aplicado via JunctionTableMigrationHelper.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit para classes de teste.")]
public sealed class AreaVisibilityConfigurationIntegrationTests : IAsyncLifetime
{
    private const string JunctionTable = "entidade_area_scoped_areas_de_interesse";
    private const string ParentFkColumn = "entidade_area_scoped_id";

    private static readonly AreaCodigo Ceps = AreaCodigo.From("CEPS").Value!;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TMeio = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_area_visibility_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        // btree_gist + o exclusion constraint GIST não vêm do modelo EF —
        // em produção a primeira migration os emite; aqui replicamos o passo.
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await ExecutarAsync(conexao, "CREATE EXTENSION IF NOT EXISTS btree_gist;");
        await ExecutarAsync(
            conexao,
            JunctionTableMigrationHelper.ExclusionConstraintSql(JunctionTable, ParentFkColumn));
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "GIST — janelas de validade disjuntas para o mesmo (pai, área) são aceitas")]
    public async Task Gist_JanelasDisjuntas_SaoAceitas()
    {
        await using TestDbContext context = CriarContext();
        EntidadeAreaScopedFicticia pai = await SemearEntidadeAsync(context);

        context.Bindings.Add(Binding(pai.Id, Ceps, T0, T1));
        context.Bindings.Add(Binding(pai.Id, Ceps, T2, validoAte: null));

        Func<Task> salvar = () => context.SaveChangesAsync();

        await salvar.Should().NotThrowAsync("[T0,T1) e [T2,∞) não se sobrepõem");
    }

    [Fact(DisplayName = "GIST — janelas sobrepostas para o mesmo (pai, área) violam o exclusion constraint")]
    public async Task Gist_JanelasSobrepostas_ViolamOConstraint()
    {
        await using TestDbContext context = CriarContext();
        EntidadeAreaScopedFicticia pai = await SemearEntidadeAsync(context);

        context.Bindings.Add(Binding(pai.Id, Ceps, T0, T1));
        context.Bindings.Add(Binding(pai.Id, Ceps, TMeio, validoAte: null));

        Func<Task> salvar = () => context.SaveChangesAsync();

        (await salvar.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException<DbUpdateException, PostgresException>())
            .Which.SqlState.Should().Be(
                PostgresErrorCodes.ExclusionViolation,
                "[T0,T1) e [TMeio,∞) se sobrepõem");
    }

    [Fact(DisplayName = "Índice parcial — ix_{junction}_vigentes existe filtrando valid_to IS NULL")]
    public async Task IndiceParcial_DosVinculosVigentes_Existe()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using NpgsqlCommand cmd = new(
            "SELECT indexdef FROM pg_indexes WHERE indexname = @nome",
            conexao);
        cmd.Parameters.AddWithValue("nome", $"ix_{JunctionTable}_vigentes");

        object? indexDef = await cmd.ExecuteScalarAsync();

        indexDef.Should().NotBeNull("o índice parcial dos vínculos vigentes deve existir");
        ((string)indexDef!).Should().Contain("valid_to IS NULL");
    }

    [Fact(DisplayName = "FK — binding com parent_id inexistente viola foreign key")]
    public async Task Fk_ParentIdInexistente_ViolaForeignKey()
    {
        await using TestDbContext context = CriarContext();
        context.Bindings.Add(Binding(Guid.CreateVersion7(), Ceps, T0, validoAte: null));

        Func<Task> salvar = () => context.SaveChangesAsync();

        (await salvar.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException<DbUpdateException, PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact(DisplayName = "ON DELETE RESTRICT — apagar a entidade pai com bindings é bloqueado pelo banco")]
    public async Task OnDeleteRestrict_ApagarPaiComBindings_EBloqueado()
    {
        Guid paiId;
        await using (TestDbContext semeador = CriarContext())
        {
            EntidadeAreaScopedFicticia pai = EntidadeAreaScopedFicticia.Criar("Entidade de teste");
            semeador.Entidades.Add(pai);
            semeador.Bindings.Add(Binding(pai.Id, Ceps, T0, validoAte: null));
            await semeador.SaveChangesAsync();
            paiId = pai.Id;
        }

        // Context novo: o binding não está rastreado, então o EF emite o
        // DELETE e a FK ON DELETE RESTRICT do banco é quem bloqueia.
        await using TestDbContext context = CriarContext();
        EntidadeAreaScopedFicticia paraApagar = await context.Entidades.SingleAsync(e => e.Id == paiId);
        context.Entidades.Remove(paraApagar);
        Func<Task> apagar = () => context.SaveChangesAsync();

        // 23001 (restrict_violation), não 23503: o PG levanta esse código
        // específico justamente porque a FK foi criada com ON DELETE RESTRICT
        // literal — o que confirma que o EF não degradou para NO ACTION.
        (await apagar.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException<DbUpdateException, PostgresException>())
            .Which.SqlState.Should().Be(
                PostgresErrorCodes.RestrictViolation,
                "ADR-0060: a FK da junction é ON DELETE RESTRICT");
    }

    private static AreaDeInteresseBinding<EntidadeAreaScopedFicticia> Binding(
        Guid parentId, AreaCodigo area, DateTimeOffset validoDe, DateTimeOffset? validoAte)
    {
        AreaDeInteresseBinding<EntidadeAreaScopedFicticia> binding =
            AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.Criar(parentId, area, validoDe, "sub-teste");
        if (validoAte is not null)
        {
            binding.Encerrar(validoAte.Value);
        }

        return binding;
    }

    private static async Task<EntidadeAreaScopedFicticia> SemearEntidadeAsync(TestDbContext context)
    {
        EntidadeAreaScopedFicticia pai = EntidadeAreaScopedFicticia.Criar("Entidade de teste");
        context.Entidades.Add(pai);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return pai;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DDL de teste com entrada controlada — literais e o SQL gerado por "
            + "JunctionTableMigrationHelper; não há entrada de usuário.")]
    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand cmd = new(sql, conexao);
        await cmd.ExecuteNonQueryAsync();
    }

    private TestDbContext CriarContext()
    {
        DbContextOptionsBuilder<TestDbContext> options = new();
        options.UseNpgsql(_postgres.GetConnectionString());
        return new TestDbContext(options.Options);
    }

    // ─── Fixtures de teste ─────────────────────────────────────────────────

    private sealed class EntidadeAreaScopedFicticia : EntityBase, IAreaScopedEntity
    {
        public AreaCodigo? Proprietario { get; private set; }

        public string Nome { get; private set; } = string.Empty;

        private EntidadeAreaScopedFicticia()
        {
        }

        public static EntidadeAreaScopedFicticia Criar(string nome) => new() { Nome = nome };
    }

    private sealed class EntidadeAreaScopedFicticiaConfiguration()
        : AreaVisibilityConfiguration<EntidadeAreaScopedFicticia>("entidade_area_scoped")
    {
        public override void Configure(
            Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<EntidadeAreaScopedFicticia> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("entidades_area_scoped_ficticias");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            ConfigureAreaVisibility(builder);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada pelo EF Core e por CriarContext.")]
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<EntidadeAreaScopedFicticia> Entidades => Set<EntidadeAreaScopedFicticia>();

        public DbSet<AreaDeInteresseBinding<EntidadeAreaScopedFicticia>> Bindings =>
            Set<AreaDeInteresseBinding<EntidadeAreaScopedFicticia>>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ConfigureValueObjectConverters();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            EntidadeAreaScopedFicticiaConfiguration config = new();
            modelBuilder.ApplyConfiguration<EntidadeAreaScopedFicticia>(config);
            modelBuilder.ApplyConfiguration<AreaDeInteresseBinding<EntidadeAreaScopedFicticia>>(config);
        }
    }
}
