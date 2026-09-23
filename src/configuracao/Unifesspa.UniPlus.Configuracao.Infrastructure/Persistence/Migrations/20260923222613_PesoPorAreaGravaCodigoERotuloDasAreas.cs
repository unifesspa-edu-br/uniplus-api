using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PesoPorAreaGravaCodigoERotuloDasAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As linhas atuais guardam os pesos em cinco colunas fixas, sem código nem
            // rótulo de área, e não há produção: são excluídas, e o recadastro pela API
            // já grava no formato novo. A tabela não tem trigger nem referência por FK.
            migrationBuilder.Sql("DELETE FROM configuracao.peso_area_enem;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_peso_area_enem_corte_redacao",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropCheckConstraint(
                name: "ck_peso_area_enem_peso_ciencias_humanas",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropCheckConstraint(
                name: "ck_peso_area_enem_peso_ciencias_natureza",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropCheckConstraint(
                name: "ck_peso_area_enem_peso_linguagens",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropCheckConstraint(
                name: "ck_peso_area_enem_peso_matematica",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropCheckConstraint(
                name: "ck_peso_area_enem_peso_redacao",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropColumn(
                name: "corte_redacao",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropColumn(
                name: "peso_ciencias_humanas",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropColumn(
                name: "peso_ciencias_natureza",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropColumn(
                name: "peso_linguagens",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropColumn(
                name: "peso_matematica",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropColumn(
                name: "peso_redacao",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.CreateTable(
                name: "peso_area_enem_area",
                schema: "configuracao",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código da área do ENEM, sem abreviação e sem acento (ex.: REDACAO); identidade estável."),
                    peso_area_enem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Linha de Pesos por Área (resolução e grupo de área) a que a área pertence."),
                    rotulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Rótulo oficial da área (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código."),
                    peso = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false, comment: "Peso da área na média ponderada da nota do ENEM: multiplicador adimensional, de 0 a 99,99."),
                    corte = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true, comment: "Nota mínima da área na escala do ENEM (0 a 1000). Nulo quando a área não tem corte.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_peso_area_enem_area", x => new { x.peso_area_enem_id, x.codigo });
                    table.CheckConstraint("ck_peso_area_enem_area_codigo", "codigo IN ('REDACAO', 'CIENCIAS_DA_NATUREZA', 'CIENCIAS_HUMANAS', 'LINGUAGENS', 'MATEMATICA')");
                    table.CheckConstraint("ck_peso_area_enem_area_corte", "corte IS NULL OR (corte >= 0 AND corte <= 1000)");
                    table.CheckConstraint("ck_peso_area_enem_area_peso", "peso >= 0");
                    table.ForeignKey(
                        name: "fk_peso_area_enem_area_peso_area_enem_peso_area_enem_id",
                        column: x => x.peso_area_enem_id,
                        principalSchema: "configuracao",
                        principalTable: "peso_area_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Peso e corte de cada uma das cinco áreas do ENEM numa linha de Pesos por Área. Código e rótulo são postos pelo sistema; o operador edita só peso e corte.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem as áreas, as linhas no formato novo ficariam com pesos zerados: são
            // excluídas, como na ida.
            migrationBuilder.Sql("DELETE FROM configuracao.peso_area_enem;");

            migrationBuilder.DropTable(
                name: "peso_area_enem_area",
                schema: "configuracao");

            migrationBuilder.AddColumn<decimal>(
                name: "corte_redacao",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "numeric(7,3)",
                precision: 7,
                scale: 3,
                nullable: false,
                defaultValue: 400m);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_ciencias_humanas",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_ciencias_natureza",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_linguagens",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_matematica",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_redacao",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "ck_peso_area_enem_corte_redacao",
                schema: "configuracao",
                table: "peso_area_enem",
                sql: "corte_redacao >= 0 AND corte_redacao <= 1000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_peso_area_enem_peso_ciencias_humanas",
                schema: "configuracao",
                table: "peso_area_enem",
                sql: "peso_ciencias_humanas >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_peso_area_enem_peso_ciencias_natureza",
                schema: "configuracao",
                table: "peso_area_enem",
                sql: "peso_ciencias_natureza >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_peso_area_enem_peso_linguagens",
                schema: "configuracao",
                table: "peso_area_enem",
                sql: "peso_linguagens >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_peso_area_enem_peso_matematica",
                schema: "configuracao",
                table: "peso_area_enem",
                sql: "peso_matematica >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_peso_area_enem_peso_redacao",
                schema: "configuracao",
                table: "peso_area_enem",
                sql: "peso_redacao >= 0");
        }
    }
}
