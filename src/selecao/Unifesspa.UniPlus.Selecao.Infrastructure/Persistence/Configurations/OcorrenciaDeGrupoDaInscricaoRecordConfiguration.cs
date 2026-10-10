namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class OcorrenciaDeGrupoDaInscricaoRecordConfiguration : IEntityTypeConfiguration<OcorrenciaDeGrupoDaInscricaoRecord>
{
    public void Configure(EntityTypeBuilder<OcorrenciaDeGrupoDaInscricaoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ocorrencias_de_grupo_da_inscricao", t => t.HasComment(
            "Uma ocorrência de grupo repetível da inscrição, como cada dependente de um grupo de dependentes."));

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.GrupoCodigo).HasMaxLength(FormaDoItem.FatoCodigoMaxLength).IsRequired();
        builder.Property(o => o.MembroId)
            .IsRequired()
            .HasComment("O id estável que o formulário dá ao membro — reordenar a lista não troca a identidade dele.");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(o => o.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.InscricaoId, o.GrupoCodigo, o.MembroId })
            .IsUnique()
            .HasDatabaseName("ux_ocorrencias_de_grupo_da_inscricao_membro");
    }
}
