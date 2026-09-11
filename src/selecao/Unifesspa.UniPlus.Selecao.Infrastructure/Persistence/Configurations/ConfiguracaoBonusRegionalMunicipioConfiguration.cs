namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;

public sealed class ConfiguracaoBonusRegionalMunicipioConfiguration : IEntityTypeConfiguration<ConfiguracaoBonusRegionalMunicipio>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoBonusRegionalMunicipio> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_bonus_regional_municipio");
        builder.HasKey(m => m.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.CodigoIbge).HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength).IsRequired();
        builder.Property(m => m.Nome).HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength).IsRequired();
        builder.Property(m => m.Uf).HasMaxLength(ReferenciaCidadeGeo.UfLength).IsRequired();
    }
}
