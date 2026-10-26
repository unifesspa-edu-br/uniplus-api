namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>Configuração EF Core dos inscritos com linha válida em um lote de notas.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class InscritoDoLoteDeNotasConfiguration : IEntityTypeConfiguration<InscritoDoLoteDeNotas>
{
    public void Configure(EntityTypeBuilder<InscritoDoLoteDeNotas> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("inscritos_do_lote_de_notas", t =>
        {
            t.HasComment("A inscrição com linha válida em um lote de notas, com o número de inscrição que o INEP lhe deu. Append-only.");
            t.HasCheckConstraint("ck_inscritos_do_lote_de_notas_numero_enem", "numero_de_inscricao_no_enem ~ '^[0-9]{12}$'");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.LoteId).IsRequired();
        builder.Property(i => i.InscricaoId).IsRequired();

        builder.Property(i => i.NumeroDeInscricaoNoEnem)
            .HasMaxLength(InscritoDoLoteDeNotas.DigitosDoNumeroDeInscricao)
            .IsRequired()
            .HasComment("O número de inscrição do INEP, guardado para a conferência de um recurso.");

        // Alvo da chave estrangeira composta do lançamento: não há lançamento de quem não está no lote.
        builder.HasAlternateKey(i => new { i.LoteId, i.InscricaoId })
            .HasName("ak_inscritos_do_lote_de_notas_lote_inscricao");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(i => i.InscricaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.InscricaoId)
            .HasDatabaseName("ix_inscritos_do_lote_de_notas_inscricao");
    }
}
