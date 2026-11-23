namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("inscritos_da_importacao", t =>
        {
            t.HasComment("A inscrição com linha válida em um lote de notas e o número de inscrição atribuído pelo INEP. Os registros não são alterados ou excluídos.");
            t.HasCheckConstraint("ck_inscritos_da_importacao_numero_enem", "numero_de_inscricao_no_enem ~ '^[0-9]{12}$'");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever().HasComment("Identificador do registro de inscrito na importação.");

        builder.Property(i => i.LoteId).HasColumnName("importacao_id").IsRequired().HasComment("Identificador da importação à qual pertence o inscrito.");
        builder.Property(i => i.InscricaoId).IsRequired().HasComment("Identificador da inscrição com linha válida no arquivo.");

        builder.Property(i => i.NumeroDeInscricaoNoEnem)
            .ClassificaComo(ClassificacaoDeProtecao.Identificador)
            .HasMaxLength(InscritoDoLoteDeNotas.DigitosDoNumeroDeInscricao)
            .IsRequired()
            .HasComment("O número de inscrição do INEP, guardado para a conferência de um recurso.");

        // Alvo da chave estrangeira composta do lançamento: não há lançamento de quem não está no lote.
        builder.HasAlternateKey(i => new { i.LoteId, i.InscricaoId })
            .HasName("ak_inscritos_da_importacao_importacao_inscricao");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(i => i.InscricaoId)
            .HasConstraintName("fk_inscritos_da_importacao_inscricao")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.InscricaoId)
            .HasDatabaseName("ix_inscritos_da_importacao_inscricao");
    }
}
