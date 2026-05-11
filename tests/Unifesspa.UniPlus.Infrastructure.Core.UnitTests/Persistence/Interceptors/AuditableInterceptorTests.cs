namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Interceptors;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Kernel.Domain.Entities;
using Kernel.Domain.Interfaces;

public sealed class AuditableInterceptorTests
{
    private const string SystemUser = "system";

    private sealed class EntidadeTeste : EntityBase
    {
        public string Nome { get; set; } = string.Empty;
    }

    // Entidade que implementa IAuditableEntity (opt-in da #390) — exercita o
    // ramo do interceptor que preenche CreatedBy/UpdatedBy. Setters private
    // espelham o pattern de entidades reais (EF Core escreve via reflection
    // sem expor mutação ao domínio).
    private sealed class EntidadeAuditavel : EntityBase, IAuditableEntity
    {
        public string Nome { get; set; } = string.Empty;
        public string? CreatedBy { get; private set; }
        public string? UpdatedBy { get; private set; }
    }

    private sealed class ContextoTeste(DbContextOptions<ContextoTeste> options) : DbContext(options)
    {
        public DbSet<EntidadeTeste> Entidades { get; set; } = null!;
        public DbSet<EntidadeAuditavel> EntidadesAuditaveis { get; set; } = null!;
    }

    private static ContextoTeste CriarContexto(IUserContext? userContext = null, string? dbName = null) =>
        new(new DbContextOptionsBuilder<ContextoTeste>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableInterceptor(userContext))
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

    // ───── #390: ramos IAuditableEntity (opt-in) ──────────────────────────

    [Fact]
    public void SavingChanges_DadoIAuditableEntityAddedEUsuarioAutenticado_EntaoCreatedByDeveSerUserId()
    {
        IUserContext userContext = UsuarioAutenticado("user-42");
        using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeAuditavel entidade = new() { Nome = "novo" };

        contexto.EntidadesAuditaveis.Add(entidade);
        contexto.SaveChanges();

        entidade.CreatedBy.Should().Be("user-42");
        entidade.UpdatedBy.Should().BeNull("UpdatedBy só preenchido em Modified");
    }

    [Fact]
    public async Task SavingChangesAsync_DadoIAuditableEntityModifiedEUsuarioAutenticado_EntaoUpdatedByDeveSerUserId()
    {
        // Compartilha o nome do DB in-memory entre os dois contextos para
        // simular "mesmo banco, requests distintos". Sem isso cada CriarContexto
        // gera Guid.NewGuid() e a entidade some entre as chamadas.
        string dbName = Guid.NewGuid().ToString();

        IUserContext primeiroCriador = UsuarioAutenticado("user-1");
        await using ContextoTeste contextoCriacao = CriarContexto(primeiroCriador, dbName);
        EntidadeAuditavel entidade = new() { Nome = "original" };
        contextoCriacao.EntidadesAuditaveis.Add(entidade);
        await contextoCriacao.SaveChangesAsync();

        // Reabre o contexto com outro usuário — espelha o padrão de modificação
        // em request distinto. UpdatedBy = quem modificou; CreatedBy permanece.
        IUserContext segundoEditor = UsuarioAutenticado("user-99");
        await using ContextoTeste contextoEdicao = CriarContexto(segundoEditor, dbName);
        EntidadeAuditavel? alvo = await contextoEdicao.EntidadesAuditaveis.FindAsync(entidade.Id);
        alvo.Should().NotBeNull("entidade persistida no DB compartilhado deve ser encontrada");
        alvo!.Nome = "modificado";

        await contextoEdicao.SaveChangesAsync();

        alvo.CreatedBy.Should().Be("user-1");
        alvo.UpdatedBy.Should().Be("user-99");
    }

    [Fact]
    public void SavingChanges_DadoIAuditableEntityEUsuarioNaoAutenticado_EntaoCreatedByDeveSerSystem()
    {
        IUserContext userContext = UsuarioAnonimo();
        using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeAuditavel entidade = new() { Nome = "job" };

        contexto.EntidadesAuditaveis.Add(entidade);
        contexto.SaveChanges();

        entidade.CreatedBy.Should().Be(SystemUser);
    }

    [Fact]
    public void SavingChanges_DadoIAuditableEntityComUserIdVazio_EntaoCreatedByDeveSerSystem()
    {
        // UserId vazio (string.Empty) cai no fallback por causa do
        // padrão `UserId: { Length: > 0 }` no ResolveUserBy — pin para evitar
        // regressão se alguém trocar para `is not null`.
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.UserId.Returns(string.Empty);
        using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeAuditavel entidade = new() { Nome = "edge" };

        contexto.EntidadesAuditaveis.Add(entidade);
        contexto.SaveChanges();

        entidade.CreatedBy.Should().Be(SystemUser);
    }

    [Fact]
    public void SavingChanges_DadoEntidadeNaoAuditavel_EntaoCreatedByPermaneceForaDoModelo()
    {
        // EntidadeTeste só herda EntityBase, sem IAuditableEntity. O interceptor
        // não deve tentar tocar em CreatedBy/UpdatedBy (não existem) — apenas
        // CreatedAt/UpdatedAt continuam funcionando. Sem essa guarda o
        // interceptor lança em entry.Property("CreatedBy") porque a
        // propriedade não está no modelo do contexto.
        IUserContext userContext = UsuarioAutenticado("user-irrelevante");
        using ContextoTeste contexto = CriarContexto(userContext);
        EntidadeTeste entidade = new() { Nome = "sem-auditoria" };

        Action acao = () =>
        {
            contexto.Entidades.Add(entidade);
            contexto.SaveChanges();
        };

        acao.Should().NotThrow();
        entidade.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(-5));
    }
}
