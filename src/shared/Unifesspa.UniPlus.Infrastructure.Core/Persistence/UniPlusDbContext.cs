namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

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

    protected abstract IReadOnlyList<IContribuicaoDoModulo> Contribuicoes { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ComposicaoDoModelo.Compor(modelBuilder, Contribuicoes);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Prepara o banco do contexto: scripts <c>Pre</c>, migrations e scripts pós, sob a trava do banco. As etapas posteriores
    /// das contribuições pedem o provider da aplicação e rodam só pela preparação do host.
    /// </summary>
    public Task PrepararOBancoAsync(CancellationToken cancellationToken = default) =>
        PreparacaoDoBancoExtensions.PrepararOBancoAsync(this, ScriptsDasContribuicoes.Assemblies(Contribuicoes), cancellationToken);

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
