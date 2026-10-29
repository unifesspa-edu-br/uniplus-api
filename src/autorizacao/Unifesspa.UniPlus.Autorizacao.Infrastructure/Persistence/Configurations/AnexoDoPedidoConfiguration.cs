namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Configuração EF Core do anexo do pedido e do registro dos acessos a ele.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AnexoDoPedidoConfiguration : IEntityTypeConfiguration<AnexoDoPedido>
{
    private const int SituacaoMaxLength = 40;
    private const int ETagMaxLength = 200;
    private const int Sha256HexLength = 64;

    private static readonly ValueConverter<SituacaoDoAnexo, string> SituacaoConverter = new(
        situacao => situacao.ToCodigo(),
        codigo => SituacaoDoAnexoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<AnexoDoPedido> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pedido_anexos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Situacao).HasConversion(SituacaoConverter).HasMaxLength(SituacaoMaxLength).IsRequired();
        builder.Property(a => a.TamanhoDeclarado).IsRequired();
        builder.Property(a => a.ETag).HasColumnName("etag").HasMaxLength(ETagMaxLength);
        builder.Property(a => a.Sha256).HasColumnName("sha256").HasMaxLength(Sha256HexLength);

        builder.Ignore(a => a.ChaveDeEnvio);
        builder.Ignore(a => a.ChaveDefinitiva);

        builder.HasIndex(a => a.PedidoId).HasDatabaseName("ix_pedido_anexos_pedido");

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119): o resultado do verificador e a confirmação
        // do envio não se misturam.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
