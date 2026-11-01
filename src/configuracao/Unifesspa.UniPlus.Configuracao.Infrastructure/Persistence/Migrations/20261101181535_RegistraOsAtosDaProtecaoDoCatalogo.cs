using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistraOsAtosDaProtecaoDoCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "protecao_do_catalogo_atos",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cadastro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    classificacao_protecao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    classificacao_recomendada = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ator_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ator_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ator_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_protecao_do_catalogo_atos", x => x.id);
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_ajuste_com_recomendacao", "ato <> 'AJUSTE_RECOMENDADO' OR classificacao_recomendada IS NOT NULL");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_ato", "ato IN ('SOLICITACAO', 'APROVACAO', 'AJUSTE_RECOMENDADO', 'REPROVACAO', 'ACEITE')");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_cadastro", "cadastro IN ('TIPO_DOCUMENTO', 'FATO')");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_reprovacao_com_justificativa", "ato <> 'REPROVACAO' OR justificativa IS NOT NULL");
                });

            migrationBuilder.CreateIndex(
                name: "ix_protecao_do_catalogo_atos_ato",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                columns: new[] { "ato", "instante" });

            migrationBuilder.CreateIndex(
                name: "ix_protecao_do_catalogo_atos_item",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                columns: new[] { "cadastro", "item_id", "instante" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "protecao_do_catalogo_atos",
                schema: "configuracao");
        }
    }
}
