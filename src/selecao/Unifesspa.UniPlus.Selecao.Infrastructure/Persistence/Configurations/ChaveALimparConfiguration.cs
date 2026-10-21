namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ChaveALimparConfiguration : IEntityTypeConfiguration<ChaveALimpar>
{
    public void Configure(EntityTypeBuilder<ChaveALimpar> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("chaves_a_limpar", t => t.HasComment(
            "Chaves do armazenamento a apagar depois de uma hora: a URL de envio recria o objeto em claro até vencer."));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.ObjectKey).HasMaxLength(500).IsRequired()
            .HasComment("A chave do objeto; leva só o id do arquivo.");
        builder.Property(c => c.LimparApos)
            .HasComment("A partir de quando a chave pode ser apagada.");

        builder.HasIndex(c => c.LimparApos).HasDatabaseName("ix_chaves_a_limpar_limpar_apos");
    }
}
