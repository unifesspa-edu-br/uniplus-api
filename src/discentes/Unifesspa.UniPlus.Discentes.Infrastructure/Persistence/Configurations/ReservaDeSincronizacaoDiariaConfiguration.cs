namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ReservaDeSincronizacaoDiariaConfiguration :
    IEntityTypeConfiguration<ReservaDeSincronizacaoDiaria>
{
    public void Configure(EntityTypeBuilder<ReservaDeSincronizacaoDiaria> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sincronizacao_diaria", table => table.HasComment(
            "Reserva idempotente do gatilho diário da sincronização de vínculos discentes."));

        builder.HasKey(reserva => reserva.DataDeReferencia);

        builder.Property(reserva => reserva.DataDeReferencia)
            .HasComment("Dia único reservado para a sincronização.");

        builder.Property(reserva => reserva.Estado)
            .HasConversion<short>()
            .HasComment("Estado do ciclo: pendente, despachado, executando ou terminal.");

        builder.Property(reserva => reserva.AgendadaEm)
            .HasComment("Instante UTC em que o disparo foi agendado.");

        builder.Property(reserva => reserva.DespachadaEm)
            .HasComment("Instante UTC em que o disparo publicou a mensagem de trabalho.");

        builder.Property(reserva => reserva.IniciadaEm)
            .HasComment("Instante UTC em que a sincronização começou ou foi retomada.");

        builder.Property(reserva => reserva.ConcluidaEm)
            .HasComment("Instante UTC em que a sincronização alcançou um estado terminal.");
    }
}
