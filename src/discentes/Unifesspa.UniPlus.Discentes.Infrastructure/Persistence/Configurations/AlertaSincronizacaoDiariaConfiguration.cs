namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AlertaSincronizacaoDiariaConfiguration :
    IEntityTypeConfiguration<AlertaSincronizacaoDiaria>
{
    public void Configure(EntityTypeBuilder<AlertaSincronizacaoDiaria> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("alerta_sincronizacao_diaria", table => table.HasComment(
            "Deduplicação persistente dos alertas de dia sem início de sincronização."));

        builder.HasKey(alerta => alerta.DataDeReferencia);

        builder.Property(alerta => alerta.DataDeReferencia)
            .HasComment("Dia em que a sincronização não foi iniciada até o horário-limite.");

        builder.Property(alerta => alerta.EmitidaEm)
            .HasComment("Instante UTC em que o alerta foi registrado.");
    }
}
