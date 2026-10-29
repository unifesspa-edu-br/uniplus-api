namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Configuração EF Core das decisões do pedido, registro só acrescentado.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class DecisaoDoPedidoConfiguration : IEntityTypeConfiguration<DecisaoDoPedido>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;
    private const int SensibilidadeMaxLength = 20;

    private static readonly ValueConverter<EtapaDoPedido, string> EtapaConverter = new(
        etapa => etapa.ToCodigo(),
        codigo => EtapaDoPedidoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<ResultadoDaDecisao, string> ResultadoConverter = new(
        resultado => resultado.ToCodigo(),
        codigo => ResultadoDaDecisaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<DecisaoDoPedido> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pedido_decisoes", tabela => tabela.HasCheckConstraint(
            "ck_pedido_decisoes_motivo_na_recusa",
            "resultado <> 'recusado' OR motivo IS NOT NULL"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Etapa).HasConversion(EtapaConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Resultado).HasConversion(ResultadoConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.DecisorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DecisorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DecisorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(d => d.Motivo).HasMaxLength(PedidoDeConcessao.MotivoMaxLength);
        builder.Property(d => d.Instante).IsRequired();
        builder.Property(d => d.PermissoesAvaliadas).IsRequired();
        builder.Property(d => d.SensibilidadeAvaliada).HasMaxLength(SensibilidadeMaxLength).IsRequired();
        builder.Property(d => d.Dispensa).HasMaxLength(CodigoMaxLength);

        builder.Ignore(d => d.Decisor);

        builder.HasIndex(d => d.PedidoId).HasDatabaseName("ix_pedido_decisoes_pedido");
    }
}
