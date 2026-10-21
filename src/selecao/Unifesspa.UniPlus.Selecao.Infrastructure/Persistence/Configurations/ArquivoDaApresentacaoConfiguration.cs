namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ArquivoDaApresentacaoConfiguration : IEntityTypeConfiguration<ArquivoDaApresentacao>
{
    public void Configure(EntityTypeBuilder<ArquivoDaApresentacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("arquivos_da_apresentacao", t => t.HasComment(
            "O arquivo de uma apresentação de documento da inscrição. O conteúdo fica no armazenamento privado, cifrado; " +
            "o nome original do arquivo não é guardado."));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Formato).HasConversion<int>().IsRequired()
            .HasComment("O formato do arquivo (PDF, JPEG ou PNG), conferido contra a exigência no início do envio.");
        builder.Property(a => a.TamanhoMaximoBytes)
            .HasComment("O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite. Copiado da exigência no início do envio.");
        builder.Property(a => a.ObjectKey).HasMaxLength(500).IsRequired()
            .HasComment("A chave do objeto do envio, em claro; só o id do arquivo a compõe. Apagada na confirmação.");
        builder.Property(a => a.ObjectKeyConfirmado).HasMaxLength(500)
            .HasComment("A chave do conteúdo selado cifrado; nula enquanto pendente.");
        builder.Property(a => a.Status).HasConversion<int>().IsRequired();
        builder.Property(a => a.HashSha256).HasMaxLength(64)
            .HasComment("O SHA-256 do conteúdo em claro, calculado no servidor na confirmação.");
        builder.Ignore(a => a.TipoDeMidia);
        builder.Ignore(a => a.ChaveSelada);

        // A apresentação sai com o último arquivo; a inscrição, com tudo.
        builder.HasOne<ApresentacaoDeDocumento>()
            .WithMany()
            .HasForeignKey(a => a.ApresentacaoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(a => a.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ApresentacaoId).HasDatabaseName("ix_arquivos_da_apresentacao_apresentacao");
        builder.HasIndex(a => a.InscricaoId).HasDatabaseName("ix_arquivos_da_apresentacao_inscricao");
    }
}
