namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ArquivoAApagarConfiguration : IEntityTypeConfiguration<ArquivoAApagar>
{
    public void Configure(EntityTypeBuilder<ArquivoAApagar> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("arquivos_a_apagar", t => t.HasComment(
            "Arquivos do armazenamento a apagar depois de uma hora: a URL de envio recria o objeto em claro até vencer."));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.ObjectKey).HasMaxLength(500).IsRequired()
            .HasComment("A chave do objeto; leva só o id do arquivo.");
        builder.Property(c => c.ApagarAPartirDe)
            .HasComment("A partir de quando o arquivo pode ser apagado.");

        builder.HasIndex(c => c.ApagarAPartirDe).HasDatabaseName("ix_arquivos_a_apagar_apagar_a_partir_de");
    }
}
