namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core dos acessos aos documentos das versões, registro só acrescentado.</summary>
public sealed class LeituraDoDocumentoDaVersaoConfiguration : IEntityTypeConfiguration<LeituraDoDocumentoDaVersao>
{
    private const int IdentidadeMaxLength = 255;

    public void Configure(EntityTypeBuilder<LeituraDoDocumentoDaVersao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("leituras_do_documento_da_versao");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.LeitorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.LeitorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.LeitorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(l => l.Instante).IsRequired();

        builder.HasOne<DocumentoDaVersao>()
            .WithMany()
            .HasForeignKey(l => l.DocumentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.DocumentoId).HasDatabaseName("ix_leituras_do_documento_da_versao_documento");
    }
}
