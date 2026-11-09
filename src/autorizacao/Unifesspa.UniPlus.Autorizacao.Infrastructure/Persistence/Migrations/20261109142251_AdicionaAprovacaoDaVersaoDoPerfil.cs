using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAprovacaoDaVersaoDoPerfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_versoes_do_perfil_perfis_perfil_id",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "motivo_da_recusa",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.RenameColumn(
                name: "recusada_em",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                newName: "expirada_em");

            migrationBuilder.AddColumn<string>(
                name: "alcance",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "categoria_da_remocao",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ciclo_de_aprovacao",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "criada_por_email",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "criador_tem_concessao",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "exige_protecao_de_dados",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expira_em",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "forma_do_cancelamento",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_da_remocao",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_do_cancelamento",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origem",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "versao_do_documento",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "avisos_emitidos",
                schema: "autorizacao",
                columns: table => new
                {
                    mensagem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    fato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emitido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avisos_emitidos", x => x.mensagem_id);
                });

            migrationBuilder.CreateTable(
                name: "decisoes_da_versao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo = table.Column<int>(type: "integer", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    acrescimo_avaliado = table.Column<string[]>(type: "text[]", nullable: false),
                    exigia_protecao_de_dados = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisoes_da_versao", x => x.id);
                    table.CheckConstraint("ck_decisoes_da_versao_etapa", "etapa IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.CheckConstraint("ck_decisoes_da_versao_motivo_na_recusa", "(resultado = 'recusado') = (motivo IS NOT NULL)");
                    table.CheckConstraint("ck_decisoes_da_versao_resultado", "resultado IN ('aprovado', 'recusado')");
                    table.ForeignKey(
                        name: "fk_decisoes_da_versao_versoes_do_perfil_versao_id",
                        column: x => x.versao_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recalculos_da_aprovacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo_anterior = table.Column<int>(type: "integer", nullable: false),
                    exigia_antes = table.Column<bool>(type: "boolean", nullable: false),
                    exige_agora = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recalculos_da_aprovacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_recalculos_da_aprovacao_versoes_do_perfil_versao_id",
                        column: x => x.versao_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                columns: new[] { "codigo", "perfil_id" },
                values: new object[,]
                {
                    { "autorizacao:perfis:aprovar-funcional", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:perfis:aprovar-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_versoes_do_perfil_perfil_id_versao_do_documento",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                columns: new[] { "perfil_id", "versao_do_documento" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_versoes_do_perfil_cancelamento",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                sql: "(situacao = 'cancelada') = (forma_do_cancelamento IS NOT NULL AND cancelada_em IS NOT NULL) AND (forma_do_cancelamento IS NULL) = (cancelada_em IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_versoes_do_perfil_expiracao",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                sql: "(situacao = 'expirada') = (expirada_em IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_versoes_do_perfil_motivo_do_cancelamento",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                sql: "forma_do_cancelamento IS DISTINCT FROM 'pessoa' OR motivo_do_cancelamento IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_versoes_do_perfil_origem",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                sql: "(origem = 'remocao' AND motivo_da_remocao IS NOT NULL AND versao_do_documento IS NOT NULL AND expira_em IS NULL) OR (origem = 'proposta' AND expira_em IS NOT NULL AND motivo_da_remocao IS NULL AND versao_do_documento IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_emitidos_tipo_fato",
                schema: "autorizacao",
                table: "avisos_emitidos",
                columns: new[] { "tipo", "fato_id" });

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_versao_etapa",
                schema: "autorizacao",
                table: "decisoes_da_versao",
                columns: new[] { "versao_id", "ciclo", "etapa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recalculos_da_aprovacao_versao_id",
                schema: "autorizacao",
                table: "recalculos_da_aprovacao",
                column: "versao_id");

            migrationBuilder.AddForeignKey(
                name: "fk_versoes_do_perfil_perfis_perfil_id",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                column: "perfil_id",
                principalSchema: "autorizacao",
                principalTable: "perfis",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_versoes_do_perfil_versoes_do_perfil_perfil_id_versao_do_doc",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                columns: new[] { "perfil_id", "versao_do_documento" },
                principalSchema: "autorizacao",
                principalTable: "versoes_do_perfil",
                principalColumns: new[] { "perfil_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_versoes_do_perfil_perfis_perfil_id",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropForeignKey(
                name: "fk_versoes_do_perfil_versoes_do_perfil_perfil_id_versao_do_doc",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropTable(
                name: "avisos_emitidos",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "decisoes_da_versao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "recalculos_da_aprovacao",
                schema: "autorizacao");

            migrationBuilder.DropIndex(
                name: "ix_versoes_do_perfil_perfil_id_versao_do_documento",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropCheckConstraint(
                name: "ck_versoes_do_perfil_cancelamento",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropCheckConstraint(
                name: "ck_versoes_do_perfil_expiracao",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropCheckConstraint(
                name: "ck_versoes_do_perfil_motivo_do_cancelamento",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropCheckConstraint(
                name: "ck_versoes_do_perfil_origem",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:perfis:aprovar-funcional", new Guid("019a0000-0000-7000-8000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:perfis:aprovar-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000002") });

            migrationBuilder.DropColumn(
                name: "alcance",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "categoria_da_remocao",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "ciclo_de_aprovacao",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "criada_por_email",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "criador_tem_concessao",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "exige_protecao_de_dados",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "expira_em",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "forma_do_cancelamento",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "motivo_da_remocao",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "motivo_do_cancelamento",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "origem",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.DropColumn(
                name: "versao_do_documento",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            migrationBuilder.RenameColumn(
                name: "expirada_em",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                newName: "recusada_em");

            migrationBuilder.AddColumn<string>(
                name: "motivo_da_recusa",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_versoes_do_perfil_perfis_perfil_id",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                column: "perfil_id",
                principalSchema: "autorizacao",
                principalTable: "perfis",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
