using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuadroDeVagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "quantidade_declarada",
                schema: "selecao",
                table: "modalidades_selecionadas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "capado_em_vo",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "estouro",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "regra_ajuste_codigo",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regra_ajuste_hash",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regra_ajuste_versao",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_publicado",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "vr_final",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "vr_nominal",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "vagas_ofertadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_distribuicao_vagas_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vagas_ofertadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_vagas_ofertadas_configuracoes_distribuicao_vagas_configurac",
                        column: x => x.configuracao_distribuicao_vagas_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_distribuicao_vagas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_vagas_ofertadas_configuracao_modalidade",
                schema: "selecao",
                table: "vagas_ofertadas",
                columns: new[] { "configuracao_distribuicao_vagas_id", "modalidade_codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vagas_ofertadas",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "quantidade_declarada",
                schema: "selecao",
                table: "modalidades_selecionadas");

            migrationBuilder.DropColumn(
                name: "capado_em_vo",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "estouro",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "regra_ajuste_codigo",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "regra_ajuste_hash",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "regra_ajuste_versao",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "total_publicado",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "vr_final",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "vr_nominal",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");
        }
    }
}
