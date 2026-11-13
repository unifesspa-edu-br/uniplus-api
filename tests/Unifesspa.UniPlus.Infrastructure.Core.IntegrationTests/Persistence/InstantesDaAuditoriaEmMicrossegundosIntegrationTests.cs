namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Tempo;

/// <summary>
/// Os instantes da auditoria carimbados pelo relógio de produção sobre uma fonte que tem a 7ª casa: o valor que fica na
/// entidade em memória é o mesmo que o Postgres devolve na releitura.
/// </summary>
public sealed class InstantesDaAuditoriaEmMicrossegundosIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset NaSetimaCasa =
        new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);

    private static readonly DateTimeOffset EmMicrossegundos =
        new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_instantes_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    private readonly FonteAjustavel _fonte = new(NaSetimaCasa);

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "O instante de criação gravado às 12:00:00,1234567 fica igual em memória e na releitura, em 12:00:00,123456")]
    public async Task Criacao_NaSetimaCasa_MemoriaIgualAReleitura()
    {
        await using ContextoDeTeste contexto = await CriarContextoAsync();
        Registro registro = Registro.Criar("primeiro");
        contexto.Registros.Add(registro);
        await contexto.SaveChangesAsync();

        Registro relido = await Reler(contexto, registro.Id);

        registro.CreatedAt.Should().Be(EmMicrossegundos);
        relido.CreatedAt.Should().Be(registro.CreatedAt);
    }

    [Fact(DisplayName = "O instante de alteração gravado com a 7ª casa fica igual em memória e na releitura")]
    public async Task Alteracao_NaSetimaCasa_MemoriaIgualAReleitura()
    {
        await using ContextoDeTeste contexto = await CriarContextoAsync();
        Registro registro = Registro.Criar("primeiro");
        contexto.Registros.Add(registro);
        await contexto.SaveChangesAsync();
        _fonte.Instante = NaSetimaCasa.AddMinutes(1);
        registro.Renomear("segundo");
        await contexto.SaveChangesAsync();

        Registro relido = await Reler(contexto, registro.Id);

        registro.UpdatedAt.Should().Be(EmMicrossegundos.AddMinutes(1));
        relido.UpdatedAt.Should().Be(registro.UpdatedAt);
    }

    private static Task<Registro> Reler(ContextoDeTeste contexto, Guid id)
    {
        contexto.ChangeTracker.Clear();
        return contexto.Registros.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task<ContextoDeTeste> CriarContextoAsync()
    {
        DbContextOptions<ContextoDeTeste> opcoes = new DbContextOptionsBuilder<ContextoDeTeste>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new AuditableInterceptor(new RelogioEmMicrossegundos(_fonte)))
            .Options;
        ContextoDeTeste contexto = new(opcoes);
        await contexto.Database.EnsureCreatedAsync();
        return contexto;
    }

    private sealed class FonteAjustavel(DateTimeOffset instante) : TimeProvider
    {
        public DateTimeOffset Instante { get; set; } = instante;

        public override DateTimeOffset GetUtcNow() => Instante;
    }

    private sealed class ContextoDeTeste(DbContextOptions<ContextoDeTeste> options) : DbContext(options)
    {
        public DbSet<Registro> Registros => Set<Registro>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Registro>(b => b.ToTable("registros"));
        }
    }

    private sealed class Registro : EntityBase
    {
        public string Descricao { get; private set; } = string.Empty;

        private Registro()
        {
        }

        public static Registro Criar(string descricao) => new() { Descricao = descricao };

        public void Renomear(string descricao) => Descricao = descricao;
    }
}
