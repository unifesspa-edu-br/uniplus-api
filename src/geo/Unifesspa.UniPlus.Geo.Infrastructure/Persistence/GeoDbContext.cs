namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// <see cref="DbContext"/> do módulo Geo — banco isolado <c>uniplus_geo</c>,
/// naming snake_case (ADR-0054), read-mostly. Estreia o eixo geoespacial:
/// coordenadas mapeadas para <c>geography(Point,4326)</c> via NetTopologySuite
/// (ADR-0091). Reference data do Geo não tem soft-delete (ADR-0092) — as
/// entidades derivam de <c>EntityBase</c> puro.
/// </summary>
/// <remarks>
/// Hospeda a hierarquia de localidade DNE+IBGE (<see cref="Pais"/> →
/// <see cref="Estado"/> → … com seus satélites de indicadores e faixas de CEP) e
/// o <c>idempotency_cache</c> (entries cifradas at-rest). A entidade-sonda
/// transitória da fundação foi substituída pelas entidades reais. As demais
/// entidades de localidade (Cidade, Distrito, Bairro, Logradouro) entram nas
/// Stories seguintes da Feature de domínio.
/// </remarks>
public sealed class GeoDbContext : DbContext, IUnitOfWork
{
    public GeoDbContext(DbContextOptions<GeoDbContext> options)
        : base(options)
    {
    }

    /// <summary>Países (carga só do Brasil em produção) — topo da hierarquia (ADR-0090).</summary>
    public DbSet<Pais> Paises => Set<Pais>();

    /// <summary>Unidades federativas (UF), situadas num País.</summary>
    public DbSet<Estado> Estados => Set<Estado>();

    /// <summary>Satélite socioeconômico IBGE de cada Estado (1:1).</summary>
    public DbSet<EstadoIndicador> EstadoIndicadores => Set<EstadoIndicador>();

    /// <summary>Faixas de CEP por Estado (capital/interior).</summary>
    public DbSet<EstadoFaixaCep> EstadoFaixasCep => Set<EstadoFaixaCep>();

    /// <summary>Municípios (eixo central do Geo, referenciado por código IBGE).</summary>
    public DbSet<Cidade> Cidades => Set<Cidade>();

    /// <summary>Satélite socioeconômico IBGE de cada Cidade (1:1).</summary>
    public DbSet<CidadeIndicador> CidadeIndicadores => Set<CidadeIndicador>();

    /// <summary>Faixas de CEP por Cidade.</summary>
    public DbSet<CidadeFaixaCep> CidadeFaixasCep => Set<CidadeFaixaCep>();

    /// <summary>Distritos situados numa Cidade (sem código IBGE; chave natural cidade+nome).</summary>
    public DbSet<Distrito> Distritos => Set<Distrito>();

    /// <summary>Faixas de CEP por Distrito.</summary>
    public DbSet<DistritoFaixaCep> DistritoFaixasCep => Set<DistritoFaixaCep>();

    /// <summary>Bairros situados numa Cidade (sem código IBGE; chave natural cidade+nome).</summary>
    public DbSet<Bairro> Bairros => Set<Bairro>();

    /// <summary>Faixas de CEP por Bairro.</summary>
    public DbSet<BairroFaixaCep> BairroFaixasCep => Set<BairroFaixaCep>();

    /// <summary>Logradouros (entrada por CEP; ~1,4M linhas) — folha da hierarquia.</summary>
    public DbSet<Logradouro> Logradouros => Set<Logradouro>();

    /// <summary>Complementos de endereçamento por CEP (lado par/ímpar, faixa), sem FK a logradouro.</summary>
    public DbSet<LogradouroComplemento> LogradouroComplementos => Set<LogradouroComplemento>();

    /// <summary>CEPs exclusivos de grandes usuários (órgãos/empresas).</summary>
    public DbSet<CepGrandeUsuario> CepGrandesUsuarios => Set<CepGrandeUsuario>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do módulo
    /// para permitir gravação adjacente no outbox.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // pg_trgm sustenta os índices trigram (gin_trgm_ops) de busca acento-
        // insensível em nome_normalizado (Cidade/Logradouro, F4). Extensão trusted
        // (criável por usuário não-superusuário); a migration emite
        // CREATE EXTENSION IF NOT EXISTS — no-op em produção (init-db já a cria),
        // criada de fato no Postgres efêmero dos testes. postgis é adicionada
        // automaticamente pelo plugin NetTopologySuite (ADR-0091).
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GeoDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (idempotency_cache).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        // Convenção global de soft-delete: aplica `!IsDeleted` a todo tipo
        // ISoftDeletable. Nenhuma entidade do Geo é ISoftDeletable (ADR-0092),
        // então a convenção é no-op aqui — mantida por simetria com os demais
        // módulos e para cobrir idempotency entry (que também não é soft-delete).
        modelBuilder.AplicarFiltroGlobalSoftDelete();
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
