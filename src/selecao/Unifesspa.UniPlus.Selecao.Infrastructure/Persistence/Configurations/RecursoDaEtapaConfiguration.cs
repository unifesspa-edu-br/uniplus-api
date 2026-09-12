namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="RecursoDaEtapa"/>. <c>produto_ancora_id</c> é coluna crua, sem
/// FK: os produtos da etapa são substituídos por inteiro a cada gravação, e uma FK
/// restritiva recusaria a substituição — mesmo raciocínio de <c>regras_recurso_fase</c>.
/// </summary>
public sealed class RecursoDaEtapaConfiguration : IEntityTypeConfiguration<RecursoDaEtapa>
{
    public void Configure(EntityTypeBuilder<RecursoDaEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("recursos_da_etapa");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Ancora).HasConversion<int>().IsRequired();
        builder.Property(r => r.ProdutoAncoraId).HasColumnName("produto_ancora_id");

        builder.OwnsOne(r => r.Regra, regra =>
        {
            regra.Property(x => x.Codigo).HasColumnName("regra_codigo").HasMaxLength(120).IsRequired();
            regra.Property(x => x.Versao).HasColumnName("regra_versao").HasMaxLength(40).IsRequired();
            regra.Property(x => x.Hash).HasColumnName("regra_hash").HasMaxLength(128);
        });
        builder.Navigation(r => r.Regra).IsRequired();

        builder.OwnsOne(r => r.Args, args =>
        {
            args.Property(x => x.PrazoValor).HasColumnName("prazo_valor");
            args.Property(x => x.PrazoUnidade).HasColumnName("prazo_unidade").HasConversion<int>();
            args.Property(x => x.SuspensividadePrimeiraInstanciaValor).HasColumnName("susp_1a_valor");
            args.Property(x => x.SuspensividadePrimeiraInstanciaUnidade).HasColumnName("susp_1a_unidade").HasConversion<int>();
            args.Property(x => x.SuspensividadeSegundaInstanciaValor).HasColumnName("susp_2a_valor");
            args.Property(x => x.SuspensividadeSegundaInstanciaUnidade).HasColumnName("susp_2a_unidade").HasConversion<int>();
        });
        builder.Navigation(r => r.Args).IsRequired();

        builder.HasIndex(r => r.EtapaProcessoId);
    }
}
