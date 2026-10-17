namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AnexoDoAtoDoProcessoConfiguration : IEntityTypeConfiguration<AnexoDoAtoDoProcesso>
{
    public void Configure(EntityTypeBuilder<AnexoDoAtoDoProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("anexos_do_ato_do_processo");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.NomeDeExibicao).HasMaxLength(AnexoDoAtoDoProcesso.NomeDeExibicaoMaxLength).IsRequired();

        builder.HasOne<DocumentoAPublicar>()
            .WithMany()
            .HasForeignKey(a => a.DocumentoAPublicarId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cada documento serve a um só anexo; a posição é única dentro do ato.
        builder.HasIndex(a => a.DocumentoAPublicarId)
            .IsUnique()
            .HasDatabaseName("ux_anexos_do_ato_do_processo_documento");
        builder.HasIndex(a => new { a.AtoDoProcessoId, a.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_anexos_do_ato_do_processo_ordem");
    }
}
