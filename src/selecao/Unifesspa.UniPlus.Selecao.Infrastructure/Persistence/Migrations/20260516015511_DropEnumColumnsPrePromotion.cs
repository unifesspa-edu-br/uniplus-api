using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropEnumColumnsPrePromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo",
                table: "etapas");

            migrationBuilder.DropColumn(
                name: "tipo_processo",
                table: "editais");

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_etapa_id",
                table: "etapas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_edital_id",
                table: "editais",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only por ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Recriar tipo_processo/tipo aqui
            // induziria desenvolvedores a `database update <baseline>` em
            // staging/prod, restabelecendo enums em vez de seguir o caminho
            // de promoção #455 → constraint NOT NULL.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
