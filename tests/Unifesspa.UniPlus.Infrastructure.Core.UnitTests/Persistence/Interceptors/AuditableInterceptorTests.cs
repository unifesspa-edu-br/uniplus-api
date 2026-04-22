namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Interceptors;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class AuditableInterceptorTests
{
    private sealed class EntidadeTeste : EntityBase
    {
        public string Nome { get; set; } = string.Empty;
    }

    private sealed class ContextoTeste(DbContextOptions<ContextoTeste> options) : DbContext(options)
    {
        public DbSet<EntidadeTeste> Entidades { get; set; } = null!;
    }

    private static ContextoTeste CriarContexto() =>
        new(new DbContextOptionsBuilder<ContextoTeste>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableInterceptor())
            .Options);

    [Fact]
    public void SavingChanges_DadoEntityAdicionada_EntaoDeveDefinirCreatedAt()
    {
        using ContextoTeste contexto = CriarContexto();
        DateTimeOffset antes = DateTimeOffset.UtcNow.AddSeconds(-1);
        EntidadeTeste entidade = new() { Nome = "teste" };

        contexto.Entidades.Add(entidade);
        contexto.SaveChanges();

        entidade.CreatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public void SavingChanges_DadoEntityModificada_EntaoDeveDefinirUpdatedAt()
    {
        using ContextoTeste contexto = CriarContexto();
        EntidadeTeste entidade = new() { Nome = "original" };
        contexto.Entidades.Add(entidade);
        contexto.SaveChanges();

        entidade.Nome = "modificado";
        DateTimeOffset antes = DateTimeOffset.UtcNow.AddSeconds(-1);
        contexto.SaveChanges();

        entidade.UpdatedAt.Should().NotBeNull();
        entidade.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task SavingChangesAsync_DadoEntityAdicionada_EntaoDeveDefinirCreatedAt()
    {
        await using ContextoTeste contexto = CriarContexto();
        DateTimeOffset antes = DateTimeOffset.UtcNow.AddSeconds(-1);
        EntidadeTeste entidade = new() { Nome = "teste" };

        contexto.Entidades.Add(entidade);
        await contexto.SaveChangesAsync();

        entidade.CreatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task SavingChangesAsync_DadoEntityModificada_EntaoDeveDefinirUpdatedAt()
    {
        await using ContextoTeste contexto = CriarContexto();
        EntidadeTeste entidade = new() { Nome = "original" };
        contexto.Entidades.Add(entidade);
        await contexto.SaveChangesAsync();

        entidade.Nome = "modificado";
        DateTimeOffset antes = DateTimeOffset.UtcNow.AddSeconds(-1);
        await contexto.SaveChangesAsync();

        entidade.UpdatedAt.Should().NotBeNull();
        entidade.UpdatedAt.Should().BeOnOrAfter(antes);
    }
}
