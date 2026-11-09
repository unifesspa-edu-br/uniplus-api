namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core do documento instituidor da versão.</summary>
public sealed class DocumentoDaVersaoConfiguration : IEntityTypeConfiguration<DocumentoDaVersao>
{
    private const int SituacaoMaxLength = 40;
    private const int ETagMaxLength = 200;
    private const int Sha256HexLength = 64;

    private static readonly ValueConverter<SituacaoDoAnexo, string> SituacaoConverter = new(
        situacao => situacao.ToCodigo(),
        codigo => SituacaoDoAnexoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<DocumentoDaVersao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("documentos_da_versao");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Situacao).HasConversion(SituacaoConverter).HasMaxLength(SituacaoMaxLength).IsRequired();
        builder.Property(d => d.TamanhoDeclarado).IsRequired();
        builder.Property(d => d.ETag).HasColumnName("etag").HasMaxLength(ETagMaxLength);
        builder.Property(d => d.Sha256).HasColumnName("sha256").HasMaxLength(Sha256HexLength);

        builder.Ignore(d => d.ChaveDeEnvio);
        builder.Ignore(d => d.ChaveDefinitiva);

        // A versão tem no máximo um documento vivo; os recusados ficam como histórico.
        builder.HasIndex(d => d.VersaoId)
            .IsUnique()
            .HasFilter($"situacao <> '{SituacaoDoAnexoCodigo.Recusado}'")
            .HasDatabaseName("ux_documentos_da_versao_vivo");

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119): o resultado do verificador e a confirmação
        // do envio não se misturam.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
