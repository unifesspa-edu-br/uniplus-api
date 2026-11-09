namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// <see cref="DbContext"/> do módulo Autorização, no schema <c>autorizacao</c> do banco único,
/// naming snake_case (ADR-0054). Hospeda os perfis de acesso com as versões, vigências e documentos instituidores, as concessões, as instalações dos primeiros gestores, os pedidos de concessão com decisões, anexos e trocas de versão, as revisões periódicas dos acessos e o cache de Idempotency-Key
/// (ADR-0027) adjacente, que permite gravação atômica com o outbox.
/// </summary>
public sealed class AutorizacaoDbContext : DbContext, IAutorizacaoUnitOfWork
{
    /// <summary>Schema do módulo: tabelas, índices, FKs e idempotency_cache deste DbContext.</summary>
    public const string Schema = "autorizacao";

    public AutorizacaoDbContext(DbContextOptions<AutorizacaoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Perfil> Perfis => Set<Perfil>();

    public DbSet<VersaoDoPerfil> VersoesDosPerfis => Set<VersaoDoPerfil>();

    public DbSet<VigenciaDoPerfil> VigenciasDosPerfis => Set<VigenciaDoPerfil>();

    public DbSet<DocumentoDaVersao> DocumentosDasVersoes => Set<DocumentoDaVersao>();

    public DbSet<LeituraDoDocumentoDaVersao> LeiturasDosDocumentosDasVersoes => Set<LeituraDoDocumentoDaVersao>();

    public DbSet<Concessao> Concessoes => Set<Concessao>();

    public DbSet<Instalacao> Instalacoes => Set<Instalacao>();

    public DbSet<TentativaDeInstalacao> TentativasDeInstalacao => Set<TentativaDeInstalacao>();

    public DbSet<PedidoDeConcessao> Pedidos => Set<PedidoDeConcessao>();

    public DbSet<DecisaoDoPedido> DecisoesDosPedidos => Set<DecisaoDoPedido>();

    public DbSet<TrocaDeVersaoDoPedido> TrocasDeVersaoDosPedidos => Set<TrocaDeVersaoDoPedido>();

    public DbSet<AnexoDoPedido> AnexosDosPedidos => Set<AnexoDoPedido>();

    public DbSet<LeituraDoAnexoDoPedido> LeiturasDosAnexos => Set<LeituraDoAnexoDoPedido>();

    public DbSet<RevisaoDeAcessos> Revisoes => Set<RevisaoDeAcessos>();

    public DbSet<ItemDaRevisao> ItensDasRevisoes => Set<ItemDaRevisao>();

    /// <summary>Cache de Idempotency-Key (ADR-0027), cifrado at-rest.</summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutorizacaoDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        modelBuilder.AplicarFiltroGlobalSoftDelete();

        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public void DescartarAlteracoesNaoSalvas() => ChangeTracker.Clear();
}
