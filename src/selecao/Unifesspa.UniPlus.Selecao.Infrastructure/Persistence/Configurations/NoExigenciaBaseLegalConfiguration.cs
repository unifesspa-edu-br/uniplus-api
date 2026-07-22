namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Configuração EF Core de <see cref="NoExigenciaBaseLegal"/> (Story #920) — mesmo padrão de
/// <see cref="DocumentoExigidoBaseLegalConfiguration"/> (duas classes concretas separadas, sem
/// herança EF/TPH — decisão deliberada, ver <see cref="NoExigencia"/>).
/// </summary>
public sealed class NoExigenciaBaseLegalConfiguration : IEntityTypeConfiguration<NoExigenciaBaseLegal>
{
    private const int ReferenciaMaxLength = 500;
    private const int AbrangenciaCodigoMaxLength = 20;
    private const int StatusCodigoMaxLength = 20;
    private const int ObservacaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<NoExigenciaBaseLegal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("nos_exigencia_base_legal");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Referencia).HasMaxLength(ReferenciaMaxLength).IsRequired();

        builder.Property(b => b.Abrangencia)
            .HasConversion(AbrangenciaConverter)
            .HasMaxLength(AbrangenciaCodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion(StatusConverter)
            .HasMaxLength(StatusCodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Observacao).HasMaxLength(ObservacaoMaxLength);
    }

    private static readonly ValueConverter<TipoAbrangencia, string> AbrangenciaConverter =
        new(abrangencia => abrangencia.ToCodigo(), codigo => TipoAbrangenciaCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<StatusBaseLegal, string> StatusConverter =
        new(status => status.ToCodigo(), codigo => StatusBaseLegalCodigo.FromCodigo(codigo));
}
