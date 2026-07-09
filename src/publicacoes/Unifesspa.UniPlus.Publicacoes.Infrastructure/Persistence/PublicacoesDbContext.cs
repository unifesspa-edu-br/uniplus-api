namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// DbContext do módulo Publicações — o registro central dos atos normativos
/// publicados por Reitoria, CEPS e CRCA (ADR-0105).
///
/// Nasce sem entidades: o schema é criado vazio, e as tabelas
/// (cadastro de tipos de ato, ato normativo, vínculo ato↔entidade) chegam nas
/// stories seguintes. O módulo não conhece ProcessoSeletivo, Chamada nem
/// configuração de certame — nenhuma coluna, nenhuma chave estrangeira desses
/// conceitos entra aqui.
/// </summary>
public sealed class PublicacoesDbContext : DbContext
{
    /// <summary>
    /// Schema do módulo no banco único do monólito modular (ADR-0097). Tabelas,
    /// índices e FKs deste DbContext vivem neste schema.
    /// </summary>
    public const string Schema = "publicacoes";

    public PublicacoesDbContext(DbContextOptions<PublicacoesDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        // Banco único, schema-por-módulo.
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PublicacoesDbContext).Assembly);
        // Convenção global de soft-delete (issue #629): aplica `!IsDeleted` a todo
        // tipo ISoftDeletable, após os ApplyConfigurations registrarem os tipos.
        modelBuilder.AplicarFiltroGlobalSoftDelete();
        base.OnModelCreating(modelBuilder);
    }
}
