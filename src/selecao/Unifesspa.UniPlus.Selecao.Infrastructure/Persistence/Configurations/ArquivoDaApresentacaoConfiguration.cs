namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

internal sealed class ArquivoDaApresentacaoConfiguration : IEntityTypeConfiguration<ArquivoDaApresentacao>
{
    public void Configure(EntityTypeBuilder<ArquivoDaApresentacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("arquivos_da_apresentacao", t =>
        {
            t.HasCheckConstraint("ck_arquivos_da_apresentacao_formato", $"formato IN ({SqlDosCodigosDeContrato.Lista<FormatoPermitido>()})");
            t.HasCheckConstraint("ck_arquivos_da_apresentacao_situacao", $"situacao IN ({SqlDosCodigosDeContrato.Lista<SituacaoDoArquivoDoCandidato>()})");
            t.HasCheckConstraint("ck_arquivos_da_apresentacao_motivo_da_recusa", $"motivo_da_recusa IN ({SqlDosCodigosDeContrato.Lista<MotivoDeRecusaDoArquivo>()})");
            t.HasComment(
            "O arquivo de uma apresentação de documento da inscrição. O conteúdo fica no armazenamento privado, cifrado pelo " +
            "próprio armazenamento; o nome original do arquivo não é guardado.");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Formato).HasMaxLength(4).IsRequired()
            .HasComment("Formato do arquivo.");
        builder.Property(a => a.TamanhoMaximoBytes)
            .HasComment("O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite, e nunca acima de 20 MB. Copiado da exigência no início do envio.");
        builder.Property(a => a.ObjectKey).HasMaxLength(500).IsRequired()
            .HasComment("A chave do objeto no bucket de envio (quarentena); só o id do arquivo a compõe.");
        builder.Property(a => a.Situacao).HasMaxLength(18).IsRequired()
            .HasColumnName("situacao")
            .HasComment("Situação da verificação; só o arquivo verificado atende à exigência.");
        builder.Property(a => a.ETag).HasColumnName("etag").HasMaxLength(256)
            .HasComment("O ETag que o PUT devolveu ao candidato, enviado na confirmação; fixa a versão do objeto que o verificador confere.");
        builder.Property(a => a.Publicacoes).IsRequired()
            .HasComment("Quantas vezes o pedido de verificação foi publicado; o prazo da reconciliação dobra a cada uma.");
        builder.Property(a => a.ProximaPublicacaoEm)
            .HasComment("Quando a reconciliação pode republicar o pedido: a última publicação mais o prazo, que cresce a cada publicação.");
        builder.Property(a => a.ChaveDefinitiva).HasMaxLength(500)
            .HasComment("A chave do objeto promovido ao bucket definitivo; nula enquanto não verificado.");
        builder.Property(a => a.MotivoDaRecusa).ClassificaComo(ClassificacaoDeProtecao.Pessoal).HasMaxLength(38)
            .HasComment("Por que a verificação recusou o arquivo; nulo fora da recusa.");
        builder.Property(a => a.HashSha256).HasMaxLength(64)
            .HasComment("O SHA-256 do conteúdo, calculado pelo verificador; nulo enquanto não verificado.");
        builder.Property(a => a.ConstouDeEnvio).IsRequired()
            .HasComment("O arquivo verificado já constou de um envio da inscrição; a limpeza dos envios seguintes nunca o apaga (UNI-REQ-0168).");
        builder.Ignore(a => a.TipoDeMidia);
        builder.Ignore(a => a.ChaveDefinitivaPrevista);
        builder.Ignore(a => a.MensagemAoCandidato);

        // A apresentação sai com o último arquivo; a inscrição, com tudo.
        builder.HasOne<ApresentacaoDeDocumento>()
            .WithMany()
            .HasForeignKey(a => a.ApresentacaoId)
            .HasConstraintName("fk_arquivos_da_apresentacao_apresentacao_de_documento")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(a => a.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ApresentacaoId).HasDatabaseName("ix_arquivos_da_apresentacao_apresentacao");
        builder.HasIndex(a => a.InscricaoId).HasDatabaseName("ix_arquivos_da_apresentacao_inscricao");

        builder.HasIndex(a => a.ProximaPublicacaoEm)
            .HasDatabaseName("ix_arquivos_da_apresentacao_em_verificacao")
            .HasFilter($"situacao = {SqlDosCodigosDeContrato.Literal(SituacaoDoArquivoDoCandidato.EmVerificacao)}");
    }
}
