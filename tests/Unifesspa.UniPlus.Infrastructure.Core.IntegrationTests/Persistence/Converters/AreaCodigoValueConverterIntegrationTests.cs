namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence.Converters;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

// Valida AreaCodigo end-to-end contra PostgreSQL real, exercitando a
// convenção global ConfigureValueObjectConverters (não HasConversion manual):
// round-trip de AreaCodigo obrigatório e AreaCodigo? nullable, schema gerado
// (varchar(32)) e o fail-fast quando a coluna é corrompida.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit para classes de teste.")]
public sealed class AreaCodigoValueConverterIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_areacodigo_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "Round-trip — AreaCodigo obrigatório persiste e recupera idêntico")]
    public async Task RoundTrip_AreaCodigoObrigatorio()
    {
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        AreaCodigo codigo = AreaCodigo.From("CEPS").Value!;
        context.Entidades.Add(EntidadeComArea.Criar(codigo, proprietario: null));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        EntidadeComArea lida = await context.Entidades.AsNoTracking().SingleAsync();

        lida.Codigo.Should().Be(codigo);
        lida.ProprietarioOpcional.Should().BeNull("AreaCodigo? não preenchido persiste como NULL");
    }

    [Fact(DisplayName = "Round-trip — AreaCodigo? nullable preenchido persiste e recupera")]
    public async Task RoundTrip_AreaCodigoNullablePreenchido()
    {
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        AreaCodigo codigo = AreaCodigo.From("PROEG").Value!;
        AreaCodigo proprietario = AreaCodigo.From("PLATAFORMA").Value!;
        context.Entidades.Add(EntidadeComArea.Criar(codigo, proprietario));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        EntidadeComArea lida = await context.Entidades.AsNoTracking().SingleAsync();

        lida.Codigo.Should().Be(codigo);
        lida.ProprietarioOpcional.Should().Be(proprietario);
    }

    [Fact(DisplayName = "Schema — convenção global gera coluna varchar(32) para AreaCodigo")]
    public async Task Schema_ConvencaoGeraVarchar32()
    {
        // Confirma que ConfigureValueObjectConverters (e não um HasConversion
        // pontual) aplicou o converter + o tipo de coluna. Falha aqui = o
        // AreaCodigoValueConverter não está registrado em ValueObjectConventions.
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        await using Npgsql.NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using Npgsql.NpgsqlCommand cmd = new(
            """
            SELECT column_name, udt_name, character_maximum_length
              FROM information_schema.columns
             WHERE table_name = 'entidades_com_area' AND column_name IN ('codigo', 'proprietario')
            """,
            conexao);

        Dictionary<string, (string Udt, int? MaxLength)> colunas = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            colunas[reader.GetString(0)] = (
                reader.GetString(1),
                await reader.IsDBNullAsync(2) ? null : reader.GetInt32(2));
        }

        colunas["codigo"].Udt.Should().Be("varchar");
        colunas["codigo"].MaxLength.Should().Be(32);
        colunas["proprietario"].Udt.Should().Be("varchar");
        colunas["proprietario"].MaxLength.Should().Be(32);
    }

    [Fact(DisplayName = "Materialização — coluna corrompida lança InvalidOperationException com contexto")]
    public async Task Materializacao_DadoCorrompido_LancaComContexto()
    {
        // Simula corrupção: a coluna varchar aceita qualquer string, mas
        // '1-invalido' não passa em AreaCodigo.From (inicia por dígito, tem
        // hífen). A leitura via converter deve falhar alto com
        // InvalidOperationException citando o VO e o código do erro — não
        // produzir um default(AreaCodigo) silencioso.
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        context.Entidades.Add(EntidadeComArea.Criar(AreaCodigo.From("CEPS").Value!, proprietario: null));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await using Npgsql.NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using Npgsql.NpgsqlCommand corromper = new(
            "UPDATE entidades_com_area SET codigo = '1-invalido'", conexao);
        await corromper.ExecuteNonQueryAsync();

        Func<Task> leitura = async () => await context.Entidades.AsNoTracking().SingleAsync();

        await leitura.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains(nameof(AreaCodigo), StringComparison.Ordinal)
                     && e.Message.Contains(AreaCodigo.CodigoErroInvalido, StringComparison.Ordinal));
    }

    private TestDbContext CriarContext()
    {
        DbContextOptionsBuilder<TestDbContext> options = new();
        options.UseNpgsql(_postgres.GetConnectionString());
        return new TestDbContext(options.Options);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada pelo EF Core e por CriarContext.")]
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<EntidadeComArea> Entidades => Set<EntidadeComArea>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ConfigureValueObjectConverters();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<EntidadeComArea>(b =>
            {
                b.ToTable("entidades_com_area");
                // Apenas o nome da coluna — o converter e o tipo (varchar(32))
                // vêm da convenção global ConfigureValueObjectConverters.
                b.Property(e => e.Codigo).HasColumnName("codigo");
                b.Property(e => e.ProprietarioOpcional).HasColumnName("proprietario");
            });
        }
    }

    // Agregado de teste — herda EntityBase para ganhar Id UUIDv7 e audit;
    // existe só para exercitar o AreaCodigoValueConverter via convenção.
    private sealed class EntidadeComArea : EntityBase
    {
        public AreaCodigo Codigo { get; private set; }

        public AreaCodigo? ProprietarioOpcional { get; private set; }

        private EntidadeComArea()
        {
        }

        public static EntidadeComArea Criar(AreaCodigo codigo, AreaCodigo? proprietario) =>
            new()
            {
                Codigo = codigo,
                ProprietarioOpcional = proprietario,
            };
    }
}
