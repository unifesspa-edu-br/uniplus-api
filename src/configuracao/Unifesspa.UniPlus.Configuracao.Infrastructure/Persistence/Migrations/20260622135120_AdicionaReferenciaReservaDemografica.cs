using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaReferenciaReservaDemografica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "referencia_reserva_demografica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    censo_referencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ppi_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    quilombola_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    pcd_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referencia_reserva_demografica", x => x.id);
                    table.CheckConstraint("ck_referencia_reserva_demografica_pcd_percentual", "pcd_percentual >= 0 AND pcd_percentual <= 100");
                    table.CheckConstraint("ck_referencia_reserva_demografica_ppi_percentual", "ppi_percentual >= 0 AND ppi_percentual <= 100");
                    table.CheckConstraint("ck_referencia_reserva_demografica_quilombola_percentual", "quilombola_percentual >= 0 AND quilombola_percentual <= 100");
                });

            migrationBuilder.CreateIndex(
                name: "ix_referencia_reserva_demografica_censo_vivo",
                table: "referencia_reserva_demografica",
                column: "censo_referencia",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referencia_reserva_demografica");
        }
    }
}
