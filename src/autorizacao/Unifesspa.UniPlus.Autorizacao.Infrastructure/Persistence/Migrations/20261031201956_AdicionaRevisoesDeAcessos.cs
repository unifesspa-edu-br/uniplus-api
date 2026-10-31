using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaRevisoesDeAcessos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "revisoes_de_acessos",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    aberta_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aberta_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aberta_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisoes_de_acessos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "revisao_itens",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decide = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    decidido_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisao_itens", x => x.id);
                    table.CheckConstraint("ck_revisao_itens_decisao_completa", "(resultado IS NULL AND decidido_por_emissor IS NULL AND decidido_por_sub IS NULL AND decidido_em IS NULL) OR (resultado IS NOT NULL AND decidido_por_emissor IS NOT NULL AND decidido_por_sub IS NOT NULL AND decidido_em IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_revisao_itens_concessoes_concessao_id",
                        column: x => x.concessao_id,
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_revisao_itens_revisoes_de_acessos_revisao_id",
                        column: x => x.revisao_id,
                        principalSchema: "autorizacao",
                        principalTable: "revisoes_de_acessos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                columns: new[] { "codigo", "perfil_id" },
                values: new object[,]
                {
                    { "autorizacao:revisoes:rever-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:revisoes:conduzir", new Guid("019a0000-0000-7000-8000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_revisao_itens_concessao",
                schema: "autorizacao",
                table: "revisao_itens",
                column: "concessao_id");

            migrationBuilder.CreateIndex(
                name: "ux_revisao_itens_revisao_concessao",
                schema: "autorizacao",
                table: "revisao_itens",
                columns: new[] { "revisao_id", "concessao_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "revisao_itens",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "revisoes_de_acessos",
                schema: "autorizacao");

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:revisoes:rever-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:revisoes:conduzir", new Guid("019a0000-0000-7000-8000-000000000002") });
        }
    }
}
