namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class DocumentoAPublicarConfiguration : IEntityTypeConfiguration<DocumentoAPublicar>
{
    public void Configure(EntityTypeBuilder<DocumentoAPublicar> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("documentos_a_publicar", t =>
        {
            t.HasComment("Documentos enviados para publicação em um processo seletivo.");
            t.HasCheckConstraint("ck_documentos_a_publicar_finalidade", $"finalidade IN ({SqlDosCodigosDeContrato.Lista<FinalidadeDoDocumento>()})");
            t.HasCheckConstraint("ck_documentos_a_publicar_formato", $"formato IN ({SqlDosCodigosDeContrato.Lista<FormatoDeDocumento>()})");
            t.HasCheckConstraint("ck_documentos_a_publicar_situacao", $"situacao IN ({SqlDosCodigosDeContrato.Lista<StatusArquivoEnviado>()})");
        });
        builder.HasKey(d => d.Id);
        // Guid v7 gerado no domínio (EntityBase) — mesma convenção de ProcessoSeletivoConfiguration.
        builder.Property(d => d.Id).ValueGeneratedNever().HasComment("Identificador do documento.");

        builder.Property(d => d.Finalidade).HasMaxLength(19).HasComment("Finalidade do documento.").IsRequired();
        builder.Property(d => d.NomeArquivo).HasComment("Nome do arquivo recebido no download.").HasMaxLength(DocumentoAPublicar.NomeArquivoMaxLength).IsRequired();
        builder.Property(d => d.Formato).HasMaxLength(4).HasComment("Formato do documento.").IsRequired();
        builder.Property(d => d.ObjectKey).HasMaxLength(500).HasComment("Chave do arquivo enviado no armazenamento privado.").IsRequired();
        builder.Property(d => d.ObjectKeyConfirmado).HasMaxLength(500).HasComment("Chave da cópia gravada na confirmação, quando houver.");
        builder.Property(d => d.Situacao).HasMaxLength(10).HasComment("Situação do envio.").IsRequired();
        builder.Property(d => d.HashSha256).HasMaxLength(64).HasComment("Hash SHA-256 do arquivo confirmado, quando houver.");
        builder.Property(d => d.ExpiraEm).HasComment("Data e hora em que o prazo de envio termina.");
        builder.Property(d => d.TamanhoBytes).HasComment("Tamanho do arquivo confirmado em bytes, quando houver.");
        builder.Property(d => d.ConfirmadoEm).HasComment("Data e hora da confirmação do envio, quando houver.");
        builder.Property(d => d.ProcessoSeletivoId).HasComment("Identificador do processo seletivo para o qual o documento foi enviado. Antes da primeira publicação é o identificador do rascunho do processo, que a publicação mantém como identificador do processo; por isso a coluna não tem chave estrangeira, e a aplicação confere que o rascunho ou o processo existe.");
        builder.Ignore(d => d.Extensao);
        builder.Ignore(d => d.TipoDeMidia);

        // Sem chave estrangeira: antes da primeira publicação o dono é o rascunho, que não é a linha do processo. A
        // integridade com o dono é coordenada pelo bloqueio do rascunho (ver comentário da entidade).
        // Índice composto na ordem exata em que a leitura dos documentos de um
        // processo pede as linhas: filtra por processo e já entrega o resultado
        // ordenado, sem passo de sort. As colunas de ordenação são descendentes
        // porque a lista vai do envio mais recente para o mais antigo, e um
        // índice ascendente serviria o filtro mas deixaria o sort de volta.
        builder.HasIndex(d => new { d.ProcessoSeletivoId, d.CreatedAt, d.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_documentos_a_publicar_processo_recentes");
    }

}
