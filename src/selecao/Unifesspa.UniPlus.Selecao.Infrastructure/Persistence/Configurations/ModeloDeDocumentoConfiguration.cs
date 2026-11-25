namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ModeloDeDocumentoConfiguration : IEntityTypeConfiguration<ModeloDeDocumento>
{
    public void Configure(EntityTypeBuilder<ModeloDeDocumento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("modelos_de_documento", t =>
        {
            t.HasCheckConstraint("ck_modelos_de_documento_formato", $"formato IN ({SqlDosCodigosDeContrato.Lista<FormatoDeDocumento>()})");
            t.HasCheckConstraint("ck_modelos_de_documento_situacao", $"situacao IN ({SqlDosCodigosDeContrato.Lista<StatusArquivoEnviado>()})");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.NomeArquivo).HasMaxLength(ModeloDeDocumento.NomeArquivoMaxLength).IsRequired();
        builder.Property(m => m.Formato).HasMaxLength(4).HasComment("Formato do documento.").IsRequired();
        builder.Property(m => m.ObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(m => m.ObjectKeyConfirmado).HasMaxLength(500);
        builder.Property(m => m.Situacao).HasMaxLength(10).HasComment("Situação do envio.").IsRequired();
        builder.Property(m => m.HashSha256).HasMaxLength(64);
        builder.Ignore(m => m.Extensao);
        builder.Ignore(m => m.ContentType);

        // Vinculado ao processo por FK, sem navegação: não é filho do agregado.
        builder.HasOne<ProcessoSeletivo>()
            .WithMany()
            .HasForeignKey(m => m.ProcessoSeletivoId)
            .HasConstraintName("fk_modelos_de_documento_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.ProcessoSeletivoId).HasDatabaseName("ix_modelos_de_documento_processo");
    }

}
