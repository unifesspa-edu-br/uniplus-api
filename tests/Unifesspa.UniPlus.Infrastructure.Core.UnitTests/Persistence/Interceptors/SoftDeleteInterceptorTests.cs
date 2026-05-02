namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Interceptors;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class SoftDeleteInterceptorTests
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
            .AddInterceptors(new SoftDeleteInterceptor())
            .Options);

    [Fact]
    public void SavingChanges_DadoEntityRemovida_EntaoDeveConverterParaModificadaEMarcarComoExcluida()
    {
        using ContextoTeste contexto = CriarContexto();
        EntidadeTeste entidade = new() { Nome = "para excluir" };
        contexto.Entidades.Add(entidade);
        contexto.SaveChanges();

        contexto.Entidades.Remove(entidade);
        contexto.SaveChanges();

        entidade.IsDeleted.Should().BeTrue();
        entidade.DeletedAt.Should().NotBeNull();
        contexto.Entidades.Find(entidade.Id).Should().NotBeNull();
    }

    [Fact]
    public async Task SavingChangesAsync_DadoEntityRemovida_EntaoDeveConverterParaModificadaEMarcarComoExcluida()
    {
        await using ContextoTeste contexto = CriarContexto();
        EntidadeTeste entidade = new() { Nome = "para excluir" };
        contexto.Entidades.Add(entidade);
        await contexto.SaveChangesAsync();

        contexto.Entidades.Remove(entidade);
        await contexto.SaveChangesAsync();

        entidade.IsDeleted.Should().BeTrue();
        entidade.DeletedAt.Should().NotBeNull();
        (await contexto.Entidades.FindAsync(entidade.Id)).Should().NotBeNull();
    }
}
