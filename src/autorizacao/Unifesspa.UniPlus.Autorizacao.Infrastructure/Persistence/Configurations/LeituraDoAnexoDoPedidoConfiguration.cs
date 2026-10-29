namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Configuração EF Core dos acessos aos anexos do pedido, registro só acrescentado.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class LeituraDoAnexoDoPedidoConfiguration : IEntityTypeConfiguration<LeituraDoAnexoDoPedido>
{
    private const int IdentidadeMaxLength = 255;

    public void Configure(EntityTypeBuilder<LeituraDoAnexoDoPedido> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pedido_anexo_leituras");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.LeitorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.LeitorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.LeitorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(l => l.Instante).IsRequired();

        builder.HasOne<PedidoDeConcessao>()
            .WithMany()
            .HasForeignKey(l => l.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AnexoDoPedido>()
            .WithMany()
            .HasForeignKey(l => l.AnexoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.PedidoId).HasDatabaseName("ix_pedido_anexo_leituras_pedido");
    }
}
