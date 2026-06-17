namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="CepGrandeUsuario"/> — CEP exclusivo de órgão/empresa. A
/// chave natural <c>cep</c> é UNIQUE (um grande usuário por CEP). Não há cidade/UF
/// nem FK: a localização é resolvida por faixa de CEP no lookup (F4).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CepGrandeUsuarioConfiguration : IEntityTypeConfiguration<CepGrandeUsuario>
{
    public void Configure(EntityTypeBuilder<CepGrandeUsuario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("cep_grande_usuario");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Cep).IsRequired();
        builder.Property(g => g.Nome).IsRequired();

        builder.HasIndex(g => g.Cep)
            .IsUnique()
            .HasDatabaseName("ix_cep_grande_usuario_cep");

        builder.ConfigurarProveniencia(g => g.VersaoDataset, g => g.Vigente);
        builder.HasIndex(g => g.VersaoDataset)
            .HasDatabaseName("ix_cep_grande_usuario_versao_dataset");
    }
}
