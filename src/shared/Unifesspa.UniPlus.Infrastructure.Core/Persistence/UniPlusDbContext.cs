namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Base do contexto único dos módulos internos do monólito. O modelo é sempre montado completo, com a contribuição de cada
/// módulo, e é a mesma instância do escopo que repositórios, leitores e unidade de trabalho recebem.
/// </summary>
public abstract class UniPlusDbContext : DbContext, IUnitOfWork
{
    public const string EsquemaDaPlataforma = "plataforma";

    protected UniPlusDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected abstract IReadOnlyList<IContribuicaoDoModulo> Modulos { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ComposicaoDoModelo.Compor(modelBuilder, Modulos);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public void DescartarAlteracoesNaoSalvas()
    {
        ChangeTracker.Clear();
    }

    public async Task ForcarChecagemImediataDeConstraintsAsync(CancellationToken cancellationToken = default)
    {
        await Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE", cancellationToken).ConfigureAwait(false);
    }
}
