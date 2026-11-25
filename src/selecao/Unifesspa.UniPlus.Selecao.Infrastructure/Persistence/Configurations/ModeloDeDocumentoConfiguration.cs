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
            t.HasComment("Modelos editáveis oferecidos nas exigências de um processo seletivo.");
            t.HasCheckConstraint("ck_modelos_de_documento_formato", $"formato IN ({SqlDosCodigosDeContrato.Lista<FormatoDeDocumento>()})");
            t.HasCheckConstraint("ck_modelos_de_documento_situacao", $"situacao IN ({SqlDosCodigosDeContrato.Lista<StatusArquivoEnviado>()})");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever().HasComment("Identificador do modelo.");
        builder.Property(m => m.NomeArquivo).HasComment("Nome do arquivo recebido no download.").HasMaxLength(ModeloDeDocumento.NomeArquivoMaxLength).IsRequired();
        builder.Property(m => m.Formato).HasMaxLength(4).HasComment("Formato do documento.").IsRequired();
        builder.Property(m => m.ObjectKey).HasMaxLength(500).HasComment("Chave do arquivo enviado no armazenamento privado.").IsRequired();
        builder.Property(m => m.ObjectKeyConfirmado).HasMaxLength(500).HasComment("Chave da cópia gravada na confirmação, quando houver.");
        builder.Property(m => m.Situacao).HasMaxLength(10).HasComment("Situação do envio.").IsRequired();
        builder.Property(m => m.HashSha256).HasMaxLength(64).HasComment("Hash SHA-256 do arquivo confirmado, quando houver.");
        builder.Property(m => m.ExpiraEm).HasComment("Data e hora em que o prazo de envio termina.");
        builder.Property(m => m.TamanhoBytes).HasComment("Tamanho do arquivo confirmado em bytes, quando houver.");
        builder.Property(m => m.ConfirmadoEm).HasComment("Data e hora da confirmação do envio, quando houver.");
        builder.Property(m => m.ProcessoSeletivoId).HasComment("Identificador do processo seletivo que oferece o modelo nas exigências. Antes da primeira publicação é o identificador do rascunho do processo, que a publicação mantém como identificador do processo; por isso a coluna não tem chave estrangeira, e a aplicação confere que o rascunho ou o processo existe.");
        builder.Ignore(m => m.Extensao);
        builder.Ignore(m => m.ContentType);

        builder.HasIndex(m => m.ProcessoSeletivoId).HasDatabaseName("ix_modelos_de_documento_processo");
    }

}
