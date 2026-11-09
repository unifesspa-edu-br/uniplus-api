using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaConcessaoPresaAVersao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pedidos_de_concessao_perfil_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_decisao_com_beneficiario",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_encerramento",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.AddColumn<Guid>(
                name: "versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "versao_id",
                schema: "autorizacao",
                table: "concessoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trocas_de_versao_do_pedido",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_anterior = table.Column<int>(type: "integer", nullable: false),
                    versao_nova = table.Column<int>(type: "integer", nullable: false),
                    desfecho = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trocas_de_versao_do_pedido", x => x.id);
                    table.ForeignKey(
                        name: "fk_trocas_de_versao_do_pedido_pedidos_de_concessao_pedido_id",
                        column: x => x.pedido_id,
                        principalSchema: "autorizacao",
                        principalTable: "pedidos_de_concessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_de_concessao_perfil_id_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "perfil_id", "versao_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_decisao_com_beneficiario",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                sql: "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'cancelado', 'devolvido') OR beneficiario_sub IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_encerramento",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                sql: "(situacao IN ('aprovado', 'recusado', 'cancelado', 'devolvido')) = (encerrado_em IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_concessoes_perfil_versao",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "perfil_id", "versao_id" },
                filter: "revogada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trocas_de_versao_do_pedido_pedido",
                schema: "autorizacao",
                table: "trocas_de_versao_do_pedido",
                column: "pedido_id");

            migrationBuilder.AddForeignKey(
                name: "fk_concessoes_versoes_dos_perfis_perfil_id_versao_id",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "perfil_id", "versao_id" },
                principalSchema: "autorizacao",
                principalTable: "versoes_do_perfil",
                principalColumns: new[] { "perfil_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidos_de_concessao_versoes_dos_perfis_perfil_id_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "perfil_id", "versao_id" },
                principalSchema: "autorizacao",
                principalTable: "versoes_do_perfil",
                principalColumns: new[] { "perfil_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_concessoes_versoes_dos_perfis_perfil_id_versao_id",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropForeignKey(
                name: "fk_pedidos_de_concessao_versoes_dos_perfis_perfil_id_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropTable(
                name: "trocas_de_versao_do_pedido",
                schema: "autorizacao");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_de_concessao_perfil_id_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_decisao_com_beneficiario",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_encerramento",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropIndex(
                name: "ix_concessoes_perfil_versao",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropColumn(
                name: "versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropColumn(
                name: "versao_id",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_de_concessao_perfil_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                column: "perfil_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_decisao_com_beneficiario",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                sql: "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'cancelado') OR beneficiario_sub IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_encerramento",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                sql: "(situacao IN ('aprovado', 'recusado', 'cancelado')) = (encerrado_em IS NOT NULL)");
        }
    }
}
