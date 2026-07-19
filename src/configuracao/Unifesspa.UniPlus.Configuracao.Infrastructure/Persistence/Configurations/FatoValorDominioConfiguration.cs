namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Configuração EF Core de <see cref="FatoValorDominio"/> — a tabela
/// <c>fato_valor_dominio</c> (ADR-0116), filha de <c>rol_de_fatos_candidato</c>.
/// </summary>
/// <remarks>
/// A FK para <c>rol_de_fatos_candidato</c> é <strong>intra-schema</strong> (ambas
/// as tabelas vivem no schema <c>configuracao</c>, do mesmo <c>DbContext</c>) —
/// não é a FK cross-schema que o ADR-0061/fitness test
/// <c>FatoCandidatoCatalogoTests.Migrations_SemFkParaFatoCandidato</c> proíbe (essa
/// regra mira outros módulos referenciando o catálogo por FK; aqui é o próprio
/// agregado pai e seu filho, dentro do mesmo módulo). Assim como o pai, é
/// seed-governada e append-only: sem CRUD, escrita só via
/// <see cref="FatoCandidato.AdicionarValorDominio"/> no seed.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class FatoValorDominioConfiguration : IEntityTypeConfiguration<FatoValorDominio>
{
    public void Configure(EntityTypeBuilder<FatoValorDominio> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("fato_valor_dominio");

        builder.HasKey(v => v.Id);

        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever força o
        // EF a tratar a chave como fornecida pela aplicação (convenção do repo).
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Codigo).HasMaxLength(FatoValorDominio.CodigoMaxLength).IsRequired();
        builder.Property(v => v.Descricao).HasMaxLength(FatoValorDominio.DescricaoMaxLength);
        builder.Property(v => v.Ordem).IsRequired();
        builder.Property(v => v.Ativo).IsRequired();

        // Unicidade (FatoCandidatoId, Codigo) — código normalizado (trim, ordinal),
        // garantida também pela factory (FatoCandidato.AdicionarValorDominio).
        builder.HasIndex(v => new { v.FatoCandidatoId, v.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_fato_valor_dominio_fato_codigo");

        builder.HasData(MaterializarSeed());
    }

    /// <summary>
    /// Projeta o seed (<see cref="FatoValorDominioSeed.Itens"/>) para linhas que o
    /// <c>HasData</c> congela como literais na migration — mesmo padrão de
    /// <c>FatoCandidatoConfiguration.MaterializarSeed</c>.
    /// </summary>
    private static IEnumerable<object> MaterializarSeed()
    {
        // Instante-âncora fixo do seed (HasData exige valor determinístico) — o
        // mesmo instante usado pelo FatoCandidato pai.
        DateTimeOffset seedCriadoEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        return FatoValorDominioSeed.Itens.Select(item => new
        {
            item.Id,
            item.FatoCandidatoId,
            item.Codigo,
            item.Descricao,
            item.Ordem,
            item.Ativo,
            CreatedAt = seedCriadoEm,
        });
    }
}
