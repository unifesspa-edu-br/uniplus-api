namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence.Converters;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// Valida que ValueObjectConventions registra os converters corretamente e
// que o round-trip persist → recuperar preserva os Value Objects do Kernel.
// Container Postgres real (não in-memory) para garantir que tipos de
// coluna definidos pela convenção (varchar(11), jsonb, numeric(9,4))
// sejam aceitos pelo provider Npgsql.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit para classes de teste.")]
public sealed class ValueObjectConvertersIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_converters_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "Round-trip — Cpf é persistido como 11 dígitos e recuperado idêntico")]
    public async Task RoundTrip_Cpf()
    {
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        Cpf cpf = Cpf.Criar("529.982.247-25").Value!;
        EntidadeComVOs entidade = EntidadeComVOs.Criar(
            cpf: cpf,
            email: Email.Criar("teste@unifesspa.edu.br").Value!,
            nomeSocial: NomeSocial.Criar("Maria das Dores").Value!,
            nota: NotaFinal.Criar(7.5m).Value!);

        context.Entidades.Add(entidade);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        EntidadeComVOs lida = await context.Entidades.AsNoTracking().SingleAsync();

        lida.Cpf.Valor.Should().Be("52998224725");
        lida.Cpf.Should().Be(cpf, "Cpf é record — igualdade por valor (campo Valor)");
    }

    [Fact(DisplayName = "Round-trip — Email é persistido normalizado em lowercase")]
    public async Task RoundTrip_Email_NormalizadoEmLowercase()
    {
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        // VO normaliza para lowercase antes de persistir; o converter
        // apenas extrai o `.Valor` já normalizado.
        Email email = Email.Criar("Teste.Mixed@Unifesspa.EDU.BR").Value!;
        EntidadeComVOs entidade = EntidadeComVOs.Criar(
            cpf: Cpf.Criar("52998224725").Value!,
            email: email,
            nomeSocial: NomeSocial.Criar("Fulano").Value!,
            nota: NotaFinal.Criar(0m).Value!);

        context.Entidades.Add(entidade);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        EntidadeComVOs lida = await context.Entidades.AsNoTracking().SingleAsync();

        lida.Email.Valor.Should().Be("teste.mixed@unifesspa.edu.br");
    }

    [Fact(DisplayName = "Round-trip — NomeSocial com nome social preenchido preserva nome civil + nome")]
    public async Task RoundTrip_NomeSocial_ComNomeSocial()
    {
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        NomeSocial nomeSocial = NomeSocial.Criar("João Silva", "Joana Silva").Value!;
        EntidadeComVOs entidade = EntidadeComVOs.Criar(
            cpf: Cpf.Criar("52998224725").Value!,
            email: Email.Criar("a@b.com").Value!,
            nomeSocial: nomeSocial,
            nota: NotaFinal.Criar(5m).Value!);

        context.Entidades.Add(entidade);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        EntidadeComVOs lida = await context.Entidades.AsNoTracking().SingleAsync();

        lida.NomeSocial.NomeCivil.Should().Be("João Silva");
        lida.NomeSocial.Nome.Should().Be("Joana Silva");
        lida.NomeSocial.UsaNomeSocial.Should().BeTrue();
        lida.NomeSocial.NomeExibicao.Should().Be("Joana Silva");
    }

    [Fact(DisplayName = "Round-trip — NomeSocial sem nome social preserva null no campo Nome")]
    public async Task RoundTrip_NomeSocial_SemNomeSocial()
    {
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        NomeSocial nomeSocial = NomeSocial.Criar("Carlos Pereira").Value!;
        EntidadeComVOs entidade = EntidadeComVOs.Criar(
            cpf: Cpf.Criar("52998224725").Value!,
            email: Email.Criar("a@b.com").Value!,
            nomeSocial: nomeSocial,
            nota: NotaFinal.Criar(0m).Value!);

        context.Entidades.Add(entidade);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        EntidadeComVOs lida = await context.Entidades.AsNoTracking().SingleAsync();

        lida.NomeSocial.NomeCivil.Should().Be("Carlos Pereira");
        lida.NomeSocial.Nome.Should().BeNull();
        lida.NomeSocial.UsaNomeSocial.Should().BeFalse();
        lida.NomeSocial.NomeExibicao.Should().Be("Carlos Pereira");
    }

    [Fact(DisplayName = "Round-trip — NotaFinal preserva precisão decimal 4 casas após round-trip Postgres")]
    public async Task RoundTrip_NotaFinal()
    {
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        NotaFinal nota = NotaFinal.Criar(8.7654m).Value!;
        EntidadeComVOs entidade = EntidadeComVOs.Criar(
            cpf: Cpf.Criar("52998224725").Value!,
            email: Email.Criar("a@b.com").Value!,
            nomeSocial: NomeSocial.Criar("Teste").Value!,
            nota: nota);

        context.Entidades.Add(entidade);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        EntidadeComVOs lida = await context.Entidades.AsNoTracking().SingleAsync();

        lida.NotaFinal.Valor.Should().Be(8.7654m);
        lida.NotaFinal.Should().Be(nota);
    }

    [Fact(DisplayName = "Materialização — dado corrompido no banco (CPF inválido) lança InvalidOperationException com contexto")]
    public async Task Materializacao_CpfCorrompido_LancaComContexto()
    {
        // Simula corrupção: insere bytes válidos para o schema (varchar(11))
        // mas inválidos para o VO (dígitos verificadores errados). A leitura
        // via converter deve falhar alto com InvalidOperationException
        // citando o VO e o código do erro do Result — não NRE silenciosa.

        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        // Inserção legítima primeiro para garantir que existe um registro.
        EntidadeComVOs entidade = EntidadeComVOs.Criar(
            cpf: Cpf.Criar("52998224725").Value!,
            email: Email.Criar("a@b.com").Value!,
            nomeSocial: NomeSocial.Criar("Teste").Value!,
            nota: NotaFinal.Criar(0m).Value!);
        context.Entidades.Add(entidade);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Corrompe a coluna CPF via SQL direto — bypass do converter.
        await using Npgsql.NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using Npgsql.NpgsqlCommand corromper = new(
            "UPDATE entidades_com_vos SET cpf = '00000000000'", conexao);
        await corromper.ExecuteNonQueryAsync();

        Func<Task> leitura = async () => await context.Entidades.AsNoTracking().SingleAsync();

        await leitura.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains(nameof(Cpf), StringComparison.Ordinal)
                     && e.Message.Contains("Cpf.Invalido", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Schema gerado — Cpf é varchar(11), Email max=254, NomeSocial é jsonb, NotaFinal é numeric(9,4)")]
    public async Task Schema_RespeitaConvencoes()
    {
        await using TestDbContext context = CriarContext();
        await context.Database.EnsureCreatedAsync();

        // Consulta information_schema para confirmar que as convenções foram
        // aplicadas ao DDL gerado pelo EF Core. Falhas aqui indicam que o
        // ValueObjectConventions não está sendo invocado pelo DbContext de
        // produção — sinal direto para os módulos.
        await using Npgsql.NpgsqlConnection conexao =
            new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        Dictionary<string, (string TipoUdt, int? CharMax, int? NumPrec, int? NumScale)> colunas = await ListarColunasAsync(conexao);

        colunas["cpf"].TipoUdt.Should().Be("varchar");
        colunas["cpf"].CharMax.Should().Be(11);

        colunas["email"].CharMax.Should().Be(254);

        colunas["nome_social"].TipoUdt.Should().Be("jsonb");

        colunas["nota_final"].NumPrec.Should().Be(9);
        colunas["nota_final"].NumScale.Should().Be(4);
    }

    private static async Task<Dictionary<string, (string TipoUdt, int? CharMax, int? NumPrec, int? NumScale)>> ListarColunasAsync(
        Npgsql.NpgsqlConnection conexao)
    {
        const string sql = """
            SELECT column_name, udt_name, character_maximum_length, numeric_precision, numeric_scale
              FROM information_schema.columns
             WHERE table_name = 'entidades_com_vos'
        """;

        Dictionary<string, (string, int?, int?, int?)> resultado = [];
        await using Npgsql.NpgsqlCommand cmd = new(sql, conexao);
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(0);
            string tipo = reader.GetString(1);
            int? charMax = await reader.IsDBNullAsync(2) ? null : reader.GetInt32(2);
            int? numPrec = await reader.IsDBNullAsync(3) ? null : reader.GetInt32(3);
            int? numScale = await reader.IsDBNullAsync(4) ? null : reader.GetInt32(4);
            resultado[nome] = (tipo, charMax, numPrec, numScale);
        }
        return resultado;
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
        Justification = "Instanciada pelo EF Core.")]
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<EntidadeComVOs> Entidades => Set<EntidadeComVOs>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ConfigureValueObjectConverters();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<EntidadeComVOs>(b =>
            {
                b.ToTable("entidades_com_vos");
                b.Property<Cpf>(e => e.Cpf).HasColumnName("cpf");
                b.Property<Email>(e => e.Email).HasColumnName("email");
                b.Property<NomeSocial>(e => e.NomeSocial).HasColumnName("nome_social");
                b.Property<NotaFinal>(e => e.NotaFinal).HasColumnName("nota_final");
            });
        }
    }

    // Agregado de teste — herda EntityBase para ganhar Id UUIDv7 e audit;
    // apenas para validar persistência dos converters.
    private sealed class EntidadeComVOs : EntityBase
    {
        public Cpf Cpf { get; private set; } = default!;
        public Email Email { get; private set; } = default!;
        public NomeSocial NomeSocial { get; private set; } = default!;
        public NotaFinal NotaFinal { get; private set; } = default!;

        private EntidadeComVOs()
        {
        }

        public static EntidadeComVOs Criar(Cpf cpf, Email email, NomeSocial nomeSocial, NotaFinal nota) =>
            new()
            {
                Cpf = cpf,
                Email = email,
                NomeSocial = nomeSocial,
                NotaFinal = nota,
            };
    }
}
