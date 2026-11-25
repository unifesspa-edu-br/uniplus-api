namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Configura a persistência da base legal da regra de comprovação.</summary>
public sealed class BaseLegalDaComprovacaoConfiguration : IEntityTypeConfiguration<BaseLegalDaComprovacao>
{
    private const int ReferenciaMaxLength = 500;
    private const int AbrangenciaCodigoMaxLength = 20;
    private const int StatusCodigoMaxLength = 20;
    private const int ObservacaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<BaseLegalDaComprovacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("bases_legais_da_comprovacao", t =>
        {
            t.HasCheckConstraint("ck_bases_legais_da_comprovacao_abrangencia", $"abrangencia IN ({SqlDosCodigosDeContrato.Lista<TipoAbrangencia>()})");
            t.HasCheckConstraint("ck_bases_legais_da_comprovacao_situacao", $"situacao IN ({SqlDosCodigosDeContrato.Lista<StatusBaseLegal>()})");
        });
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Referencia).HasMaxLength(ReferenciaMaxLength).IsRequired();

        builder.Property(b => b.Abrangencia)
            .HasMaxLength(AbrangenciaCodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Situacao)
            .HasMaxLength(StatusCodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Observacao).HasMaxLength(ObservacaoMaxLength);
    }

}
