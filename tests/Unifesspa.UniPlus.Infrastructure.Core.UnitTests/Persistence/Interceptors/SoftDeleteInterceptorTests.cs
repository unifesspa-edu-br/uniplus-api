namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Interceptors;

using AwesomeAssertions;

using Kernel.Domain.Entities;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;

public sealed class SoftDeleteInterceptorTests
{
    private const string SystemUser = "system";

    // Instante fixo + relógio determinístico (ADR-0068): DeletedAt vem do
    // TimeProvider injetado, então a asserção verifica o valor EXATO.
    private static readonly DateTimeOffset Instante = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class EntidadeTeste : SoftDeletableEntity
    {
        public string Nome { get; set; } = string.Empty;
    }

    // Entidade que NÃO implementa ISoftDeletable — o interceptor não deve
    // convertê-la em soft-delete; sofre hard-delete físico (CA-06, issue #629).
    private sealed class EntidadeNaoSoft : EntityBase
    {
        public string Nome { get; set; } = string.Empty;
    }

    private sealed class ContextoTeste(DbContextOptions<ContextoTeste> options) : DbContext(options)
    {
        public DbSet<EntidadeTeste> Entidades { get; set; } = null!;
        public DbSet<EntidadeNaoSoft> EntidadesNaoSoft { get; set; } = null!;
    }

    private static ContextoTeste CriarContexto(IUserContext? userContext = null) =>
        new(new DbContextOptionsBuilder<ContextoTeste>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new SoftDeleteInterceptor(new FixedTimeProvider(Instante), userContext))
            .Options);

    private static IUserContext UsuarioAutenticado(string userId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.UserId.Returns(userId);
        return userContext;
    }

    private static IUserContext UsuarioAnonimo()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(false);
        userContext.UserId.Returns((string?)null);
        return userContext;
    }

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
        entidade.DeletedAt.Should().Be(Instante, "DeletedAt vem do TimeProvider injetado no interceptor");
        entidade.DeletedBy.Should().Be(SystemUser);
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
        entidade.DeletedAt.Should().Be(Instante, "DeletedAt vem do TimeProvider injetado no interceptor");
        entidade.DeletedBy.Should().Be(SystemUser);
        (await contexto.Entidades.FindAsync(entidade.Id)).Should().NotBeNull();
    }

    [Fact]
    public void SavingChanges_DadoUsuarioAutenticado_EntaoDeletedByDeveSerUserId()
    {
        IUserContext userContext = UsuarioAutenticado("user-42");
        using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeTeste entidade = new() { Nome = "para excluir" };
        contexto.Entidades.Add(entidade);
        contexto.SaveChanges();

        contexto.Entidades.Remove(entidade);
        contexto.SaveChanges();

        entidade.IsDeleted.Should().BeTrue();
        entidade.DeletedBy.Should().Be("user-42");
    }

    [Fact]
    public async Task SavingChangesAsync_DadoUsuarioAutenticado_EntaoDeletedByDeveSerUserId()
    {
        IUserContext userContext = UsuarioAutenticado("user-99");
        await using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeTeste entidade = new() { Nome = "para excluir" };
        contexto.Entidades.Add(entidade);
        await contexto.SaveChangesAsync();

        contexto.Entidades.Remove(entidade);
        await contexto.SaveChangesAsync();

        entidade.IsDeleted.Should().BeTrue();
        entidade.DeletedBy.Should().Be("user-99");
    }

    [Fact]
    public void SavingChanges_DadoUsuarioNaoAutenticado_EntaoDeletedByDeveSerSystem()
    {
        IUserContext userContext = UsuarioAnonimo();
        using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeTeste entidade = new() { Nome = "para excluir" };
        contexto.Entidades.Add(entidade);
        contexto.SaveChanges();

        contexto.Entidades.Remove(entidade);
        contexto.SaveChanges();

        entidade.DeletedBy.Should().Be(SystemUser);
    }

    [Fact]
    public void SavingChanges_DadoUsuarioAutenticadoComUserIdVazio_EntaoDeletedByDeveSerSystem()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.UserId.Returns(string.Empty);

        using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeTeste entidade = new() { Nome = "para excluir" };
        contexto.Entidades.Add(entidade);
        contexto.SaveChanges();

        contexto.Entidades.Remove(entidade);
        contexto.SaveChanges();

        entidade.DeletedBy.Should().Be(SystemUser);
    }

    // Cenário real quando o token validado não traz nem `sub` nem `nameidentifier`
    // (HttpUserContext.UserId resolve null), mas Identity.IsAuthenticated continua true.
    // O pattern matching de ResolveDeletedBy depende do null check ocorrer antes do
    // teste de Length — este caso fixa o contrato.
    [Fact]
    public void SavingChanges_DadoUsuarioAutenticadoComUserIdNulo_EntaoDeletedByDeveSerSystem()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.UserId.Returns((string?)null);

        using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeTeste entidade = new() { Nome = "para excluir" };
        contexto.Entidades.Add(entidade);
        contexto.SaveChanges();

        contexto.Entidades.Remove(entidade);
        contexto.SaveChanges();

        entidade.DeletedBy.Should().Be(SystemUser);
    }

    // CA-06 (issue #629): só ISoftDeletable é convertida em soft-delete.
    // Uma entidade que deriva de EntityBase mas não implementa a interface
    // é fisicamente removida — o interceptor não a intercepta.
    [Fact]
    public void SavingChanges_DadoEntidadeNaoSoftDeletableRemovida_EntaoDeveSerHardDeleteFisico()
    {
        using ContextoTeste contexto = CriarContexto();
        EntidadeNaoSoft entidade = new() { Nome = "hard delete" };
        contexto.EntidadesNaoSoft.Add(entidade);
        contexto.SaveChanges();

        contexto.EntidadesNaoSoft.Remove(entidade);
        contexto.SaveChanges();

        contexto.EntidadesNaoSoft.Find(entidade.Id).Should().BeNull(
            "entidade sem ISoftDeletable sofre hard-delete físico (CA-06)");
    }

    [Fact]
    public async Task SavingChangesAsync_DadoEntidadeNaoSoftDeletableRemovida_EntaoDeveSerHardDeleteFisico()
    {
        await using ContextoTeste contexto = CriarContexto();
        EntidadeNaoSoft entidade = new() { Nome = "hard delete" };
        contexto.EntidadesNaoSoft.Add(entidade);
        await contexto.SaveChangesAsync();

        contexto.EntidadesNaoSoft.Remove(entidade);
        await contexto.SaveChangesAsync();

        (await contexto.EntidadesNaoSoft.FindAsync(entidade.Id)).Should().BeNull(
            "entidade sem ISoftDeletable sofre hard-delete físico (CA-06)");
    }
}
