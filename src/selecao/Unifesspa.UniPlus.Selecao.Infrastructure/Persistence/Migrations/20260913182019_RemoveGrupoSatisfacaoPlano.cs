using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGrupoSatisfacaoPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grupo_satisfacao_id",
                schema: "selecao",
                table: "documentos_exigidos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "grupo_satisfacao_id",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "uuid",
                nullable: true);
        }
    }
}
