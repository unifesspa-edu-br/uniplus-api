namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Configuração EF Core do registro de geração do arquivo de envio ao INEP. Nem o arquivo nem os CPFs são guardados.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class GeracaoDeEnvioAoInepConfiguration : IEntityTypeConfiguration<GeracaoDeEnvioAoInep>
{
    private const int HashHexLength = 64;
    private const int CodigoMaxLength = 60;
    private const int VersaoMaxLength = 20;
    private const int GeradoPorMaxLength = 255;

    public void Configure(EntityTypeBuilder<GeracaoDeEnvioAoInep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("geracoes_de_envio_ao_inep", t =>
        {
            t.HasComment("O registro de que o arquivo de envio ao INEP foi gerado para uma edição do ENEM. Append-only; o arquivo e os CPFs não são guardados.");
            t.HasCheckConstraint("ck_geracoes_de_envio_ao_inep_total", "total_de_cpfs >= 0");
            t.HasCheckConstraint("ck_geracoes_de_envio_ao_inep_leiaute_hash", "leiaute_hash ~ '^[0-9a-f]{64}$'");
        });

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.ProcessoSeletivoId).IsRequired();
        builder.Property(g => g.EtapaProcessoId).IsRequired();
        builder.Property(g => g.EdicaoCodigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(g => g.LeiauteCodigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(g => g.LeiauteVersao).HasMaxLength(VersaoMaxLength).IsRequired();
        builder.Property(g => g.LeiauteHash).HasMaxLength(HashHexLength).IsFixedLength().IsRequired();
        builder.Property(g => g.GeradoPor).HasMaxLength(GeradoPorMaxLength).IsRequired().HasComment("O sub do token de quem gerou.");
        builder.Property(g => g.GeradoPorNome).HasMaxLength(GeradoPorMaxLength).HasComment("O nome de quem gerou, gravado no momento da ação.");
        builder.Property(g => g.GeradoEm).IsRequired();
        builder.Property(g => g.TotalDeCpfs).IsRequired();

        builder.HasOne<ProcessoSeletivo>()
            .WithMany()
            .HasForeignKey(g => g.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => new { g.ProcessoSeletivoId, g.EtapaProcessoId, g.EdicaoCodigo, g.GeradoEm })
            .IsDescending(false, false, false, true)
            .HasDatabaseName("ix_geracoes_de_envio_ao_inep_escopo");
    }
}
