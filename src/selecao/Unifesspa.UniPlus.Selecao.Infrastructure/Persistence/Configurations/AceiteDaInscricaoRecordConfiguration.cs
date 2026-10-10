namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AceiteDaInscricaoRecordConfiguration : IEntityTypeConfiguration<AceiteDaInscricaoRecord>
{
    public void Configure(EntityTypeBuilder<AceiteDaInscricaoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("aceites_da_inscricao", t => t.HasComment(
            "O aceite de um termo na inscrição, com a versão e o hash da versão fixada."));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.TermoCodigo).IsRequired();
        builder.Property(a => a.HashVersao).IsRequired();

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(a => a.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.InscricaoId, a.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_aceites_da_inscricao_ordem");
    }
}
