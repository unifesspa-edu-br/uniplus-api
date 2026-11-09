namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das trocas de versão do pedido, registro só acrescentado e sem dado pessoal.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TrocaDeVersaoDoPedidoConfiguration : IEntityTypeConfiguration<TrocaDeVersaoDoPedido>
{
    private const int DesfechoMaxLength = 40;

    private static readonly ValueConverter<DesfechoDaTrocaDeVersao, string> DesfechoConverter = new(
        desfecho => desfecho.ToCodigo(),
        codigo => DesfechoDaTrocaDeVersaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<TrocaDeVersaoDoPedido> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("trocas_de_versao_do_pedido");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.VersaoAnterior).IsRequired();
        builder.Property(t => t.VersaoNova).IsRequired();
        builder.Property(t => t.Desfecho).HasConversion(DesfechoConverter).HasMaxLength(DesfechoMaxLength).IsRequired();
        builder.Property(t => t.Instante).IsRequired();

        builder.HasIndex(t => t.PedidoId).HasDatabaseName("ix_trocas_de_versao_do_pedido_pedido");
    }
}
