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
    // As mesmas grandezas que `regras_recurso_fase` e as demais referências ao rol de regras
    // declaram, e as mesmas que o decodificador do envelope aceita: a janela recursal da etapa
    // guarda referência de regra igual à da fase, e larguras próprias aqui só produziriam
    // envelope que o decodificador aprova e a coluna recusa.
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int HashLength = 64;

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
            regra.Property(x => x.Codigo).HasColumnName("regra_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regra.Property(x => x.Versao).HasColumnName("regra_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regra.Property(x => x.Hash).HasColumnName("regra_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(r => r.Regra).IsRequired();

        builder.OwnsOne(r => r.Args, args =>
        {
            args.Property(x => x.PrazoValor).HasColumnName("prazo_valor").HasPrecision(18, 4).IsRequired();
            args.Property(x => x.PrazoUnidade).HasColumnName("prazo_unidade").HasConversion<int>();
            args.Property(x => x.SuspensividadePrimeiraInstanciaValor).HasColumnName("susp_1a_valor").HasPrecision(18, 4);
            args.Property(x => x.SuspensividadePrimeiraInstanciaUnidade).HasColumnName("susp_1a_unidade").HasConversion<int>();
            args.Property(x => x.SuspensividadeSegundaInstanciaValor).HasColumnName("susp_2a_valor").HasPrecision(18, 4);
            args.Property(x => x.SuspensividadeSegundaInstanciaUnidade).HasColumnName("susp_2a_unidade").HasConversion<int>();
        });
        builder.Navigation(r => r.Args).IsRequired();

        builder.HasIndex(r => r.EtapaProcessoId);
    }
}
