namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class EnderecoDaInscricaoRecordConfiguration : IEntityTypeConfiguration<EnderecoDaInscricaoRecord>
{
    public void Configure(EntityTypeBuilder<EnderecoDaInscricaoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Sensivel);

        builder.ToTable("enderecos_da_inscricao", t =>
        {
            t.HasComment("A resposta de um campo de endereço da inscrição, um componente por coluna.");
            t.HasCheckConstraint("ck_enderecos_da_inscricao_origem", "origem IN (1, 2)");
        });

        // Cada componente do endereço é valor de um fato cuja classificação real vem do fato coletado da
        // linha, resolvida pelo projetor: valem a mais alta, o padrão do tipo. Só a chave e o metadado são
        // Interno, para que uma coluna nova herde o padrão seguro.
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ClassificaComo(ClassificacaoDeProtecao.Interno).ValueGeneratedNever();
        builder.Property(e => e.InscricaoId).ClassificaComo(ClassificacaoDeProtecao.Interno);
        builder.Property(e => e.OcorrenciaId).ClassificaComo(ClassificacaoDeProtecao.Interno);

        builder.Property(e => e.FatoCodigo).ClassificaComo(ClassificacaoDeProtecao.Interno).HasMaxLength(FormaDoItem.FatoCodigoMaxLength).IsRequired();
        builder.Property(e => e.Origem).ClassificaComo(ClassificacaoDeProtecao.Interno).HasConversion<int>().IsRequired();
        builder.Property(e => e.Procedencia)
            .HasComment("Como o endereço foi preenchido no formulário: pela busca do CEP no Geo ou à mão.");

        builder.Property(e => e.CepConfirmado)
            .HasComment("O candidato confirmou que o CEP é o geral da cidade ou do bairro, quando o Geo só o resolveu por faixa.");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(e => e.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OcorrenciaDeGrupoDaInscricaoRecord>()
            .WithMany()
            .HasForeignKey(e => e.OcorrenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.InscricaoId, e.OcorrenciaId, e.FatoCodigo })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_enderecos_da_inscricao_fato");
    }
}
