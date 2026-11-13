using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTermoDeResponsabilidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "anexara_documento",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "termo_numero",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "termo_resumo",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "termo_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aceites_do_termo",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_do_termo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    resumo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    pessoa_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pessoa_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pessoa_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    aceito_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aceites_do_termo", x => x.id);
                    table.ForeignKey(
                        name: "fk_aceites_do_termo_pedidos_de_concessao_pedido_id",
                        column: x => x.pedido_id,
                        principalSchema: "autorizacao",
                        principalTable: "pedidos_de_concessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "termos_de_responsabilidade",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_vigente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ultimo_numero = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termos_de_responsabilidade", x => x.id);
                    table.CheckConstraint("ck_termos_de_responsabilidade_vigente", "(versao_vigente_id IS NULL) = (ultimo_numero = 0)");
                });

            migrationBuilder.CreateTable(
                name: "versoes_do_termo",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    termo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    resumo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ato = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    autor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    publicada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versoes_do_termo", x => x.id);
                    table.CheckConstraint("ck_versoes_do_termo_numero", "numero >= 1");
                    table.CheckConstraint("ck_versoes_do_termo_resumo", "resumo ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_versoes_do_termo_texto", "char_length(texto) BETWEEN 200 AND 20000");
                    table.ForeignKey(
                        name: "fk_versoes_do_termo_termos_de_responsabilidade_termo_id",
                        column: x => x.termo_id,
                        principalSchema: "autorizacao",
                        principalTable: "termos_de_responsabilidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "termos_na_instalacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instalacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    versao_do_termo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero = table.Column<int>(type: "integer", nullable: true),
                    registrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termos_na_instalacao", x => x.id);
                    table.CheckConstraint("ck_termos_na_instalacao_versao", "(situacao = 'sem_termo_publicado') = (versao_do_termo_id IS NULL) AND (versao_do_termo_id IS NULL) = (numero IS NULL)");
                    table.ForeignKey(
                        name: "fk_termos_na_instalacao_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_termos_na_instalacao_versoes_do_termo_versao_do_termo_id",
                        column: x => x.versao_do_termo_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_termo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "termos_de_responsabilidade",
                columns: new[] { "id", "created_at", "ultimo_numero", "updated_at", "versao_vigente_id" },
                values: new object[] { new Guid("019a0000-0000-7000-8000-0000000000a1"), new DateTimeOffset(new DateTime(2026, 11, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_de_concessao_termo_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                column: "termo_versao_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_termo_completo",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                sql: "(termo_versao_id IS NULL) = (termo_numero IS NULL) AND (termo_versao_id IS NULL) = (termo_resumo IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_aceites_do_termo_versao_do_termo_id",
                schema: "autorizacao",
                table: "aceites_do_termo",
                column: "versao_do_termo_id");

            migrationBuilder.CreateIndex(
                name: "ux_aceites_do_termo_pedido",
                schema: "autorizacao",
                table: "aceites_do_termo",
                column: "pedido_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_termos_de_responsabilidade_versao_vigente_id",
                schema: "autorizacao",
                table: "termos_de_responsabilidade",
                column: "versao_vigente_id");

            migrationBuilder.CreateIndex(
                name: "ix_termos_na_instalacao_versao_do_termo_id",
                schema: "autorizacao",
                table: "termos_na_instalacao",
                column: "versao_do_termo_id");

            migrationBuilder.CreateIndex(
                name: "ux_termos_na_instalacao_instalacao",
                schema: "autorizacao",
                table: "termos_na_instalacao",
                column: "instalacao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_versoes_do_termo_resumo",
                schema: "autorizacao",
                table: "versoes_do_termo",
                column: "resumo");

            migrationBuilder.CreateIndex(
                name: "ix_versoes_do_termo_termo_id",
                schema: "autorizacao",
                table: "versoes_do_termo",
                column: "termo_id");

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_termo_numero",
                schema: "autorizacao",
                table: "versoes_do_termo",
                column: "numero",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidos_de_concessao_versoes_do_termo_termo_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                column: "termo_versao_id",
                principalSchema: "autorizacao",
                principalTable: "versoes_do_termo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_aceites_do_termo_versoes_do_termo_versao_do_termo_id",
                schema: "autorizacao",
                table: "aceites_do_termo",
                column: "versao_do_termo_id",
                principalSchema: "autorizacao",
                principalTable: "versoes_do_termo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_termos_de_responsabilidade_versoes_do_termo_versao_vigente_",
                schema: "autorizacao",
                table: "termos_de_responsabilidade",
                column: "versao_vigente_id",
                principalSchema: "autorizacao",
                principalTable: "versoes_do_termo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pedidos_de_concessao_versoes_do_termo_termo_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropForeignKey(
                name: "fk_termos_de_responsabilidade_versoes_do_termo_versao_vigente_",
                schema: "autorizacao",
                table: "termos_de_responsabilidade");

            migrationBuilder.DropTable(
                name: "aceites_do_termo",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "termos_na_instalacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "versoes_do_termo",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "termos_de_responsabilidade",
                schema: "autorizacao");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_de_concessao_termo_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_termo_completo",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropColumn(
                name: "anexara_documento",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropColumn(
                name: "termo_numero",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropColumn(
                name: "termo_resumo",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropColumn(
                name: "termo_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao");
        }
    }
}
