using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaGatilhoDiarioDuravel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sincronizacao_diaria",
                schema: "discentes",
                columns: table => new
                {
                    data_de_referencia = table.Column<DateOnly>(type: "date", nullable: false, comment: "Dia único reservado para a sincronização."),
                    estado = table.Column<short>(type: "smallint", nullable: false, comment: "Estado do ciclo: pendente, despachado, executando ou terminal."),
                    agendada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante UTC em que o disparo foi agendado."),
                    despachada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante UTC em que o disparo publicou a mensagem de trabalho."),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante UTC em que a sincronização começou ou foi retomada."),
                    concluida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante UTC em que a sincronização alcançou um estado terminal.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sincronizacao_diaria", x => x.data_de_referencia);
                },
                comment: "Reserva idempotente do gatilho diário da sincronização de vínculos discentes.");

            migrationBuilder.CreateTable(
                name: "alerta_sincronizacao_diaria",
                schema: "discentes",
                columns: table => new
                {
                    data_de_referencia = table.Column<DateOnly>(
                        type: "date",
                        nullable: false,
                        comment: "Dia em que a sincronização não foi iniciada até o horário-limite."),
                    emitida_em = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        comment: "Instante UTC em que o alerta foi registrado.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerta_sincronizacao_diaria", x => x.data_de_referencia);
                },
                comment: "Deduplicação persistente dos alertas de dia sem início de sincronização.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerta_sincronizacao_diaria",
                schema: "discentes");

            migrationBuilder.DropTable(
                name: "sincronizacao_diaria",
                schema: "discentes");
        }
    }
}
