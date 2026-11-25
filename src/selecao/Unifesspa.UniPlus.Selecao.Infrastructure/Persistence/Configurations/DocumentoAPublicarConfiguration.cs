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
            t.HasCheckConstraint("ck_documentos_a_publicar_finalidade", $"finalidade IN ({SqlDosCodigosDeContrato.Lista<FinalidadeDoDocumento>()})");
            t.HasCheckConstraint("ck_documentos_a_publicar_formato", $"formato IN ({SqlDosCodigosDeContrato.Lista<FormatoDeDocumento>()})");
            t.HasCheckConstraint("ck_documentos_a_publicar_situacao", $"situacao IN ({SqlDosCodigosDeContrato.Lista<StatusArquivoEnviado>()})");
        });
        builder.HasKey(d => d.Id);
        // Guid v7 gerado no domínio (EntityBase) — mesma convenção de ProcessoSeletivoConfiguration.
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Finalidade).HasMaxLength(19).HasComment("Finalidade do documento.").IsRequired();
        builder.Property(d => d.NomeArquivo).HasMaxLength(DocumentoAPublicar.NomeArquivoMaxLength).IsRequired();
        builder.Property(d => d.Formato).HasMaxLength(4).HasComment("Formato do documento.").IsRequired();
        builder.Property(d => d.ObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(d => d.ObjectKeyConfirmado).HasMaxLength(500);
        builder.Property(d => d.Situacao).HasMaxLength(10).HasComment("Situação do envio.").IsRequired();
        builder.Property(d => d.HashSha256).HasMaxLength(64);
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
