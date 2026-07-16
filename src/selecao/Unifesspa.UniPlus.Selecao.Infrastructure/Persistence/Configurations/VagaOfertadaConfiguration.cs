namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="VagaOfertada"/> (issue #848/ADR-0115) —
/// entidade filha de <see cref="ConfiguracaoDistribuicaoVagas"/>, o quadro de
/// vagas materializado junto com os insumos.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class VagaOfertadaConfiguration : IEntityTypeConfiguration<VagaOfertada>
{
    private const int CodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<VagaOfertada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("vagas_ofertadas");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.ModalidadeCodigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(v => v.Quantidade).IsRequired();

        builder.HasIndex(v => new { v.ConfiguracaoDistribuicaoVagasId, v.ModalidadeCodigo })
            .IsUnique()
            .HasDatabaseName("ux_vagas_ofertadas_configuracao_modalidade");
    }
}
