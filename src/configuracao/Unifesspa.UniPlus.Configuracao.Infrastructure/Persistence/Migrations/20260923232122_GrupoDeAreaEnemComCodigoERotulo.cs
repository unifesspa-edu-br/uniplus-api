using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrupoDeAreaEnemComCodigoERotulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O grupo passa a ser gravado pelo código, e as linhas atuais guardam o rótulo
            // acentuado, que o CHECK novo recusa. Não há produção: os pesos são excluídos
            // (as áreas filhas caem pela FK em cascata) e o grupo dos cursos vai para nulo;
            // o recadastro pela API já grava no formato novo.
            migrationBuilder.Sql("DELETE FROM configuracao.peso_area_enem;");
            migrationBuilder.Sql("UPDATE configuracao.curso SET grupo_area_enem = NULL;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_peso_area_enem_grupo_curso",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropCheckConstraint(
                name: "ck_curso_grupo_area_enem",
                schema: "configuracao",
                table: "curso");

            migrationBuilder.AlterColumn<string>(
                name: "grupo_curso",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                comment: "Código do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), sem abreviação e sem acento, restrito aos quatro grupos pelo CHECK ck_peso_area_enem_grupo_curso; com a resolução, forma a chave de negócio da linha. O rótulo fica em grupo_curso_rotulo.",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "grupo_curso_rotulo",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                comment: "Rótulo do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código em grupo_curso.");

            migrationBuilder.AlterColumn<string>(
                name: "grupo_area_enem",
                schema: "configuracao",
                table: "curso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                comment: "Código do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), sem abreviação e sem acento, restrito aos quatro grupos pelo CHECK ck_curso_grupo_area_enem. O rótulo fica em grupo_area_enem_rotulo. Nulo quando o curso não declara grupo.",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grupo_area_enem_rotulo",
                schema: "configuracao",
                table: "curso",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                comment: "Rótulo do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código em grupo_area_enem. Nulo quando o curso não declara grupo.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_peso_area_enem_grupo_curso",
                schema: "configuracao",
                table: "peso_area_enem",
                sql: "grupo_curso IN ('TECNOLOGICA', 'HUMANISTICA_I', 'HUMANISTICA_II', 'SAUDE_E_BIOLOGICAS')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_curso_grupo_area_enem",
                schema: "configuracao",
                table: "curso",
                sql: "grupo_area_enem IS NULL OR grupo_area_enem IN ('TECNOLOGICA', 'HUMANISTICA_I', 'HUMANISTICA_II', 'SAUDE_E_BIOLOGICAS')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Os códigos não passam no CHECK antigo, que espera o rótulo acentuado: os
            // pesos são excluídos e o grupo dos cursos vai para nulo, como na ida.
            migrationBuilder.Sql("DELETE FROM configuracao.peso_area_enem;");
            migrationBuilder.Sql("UPDATE configuracao.curso SET grupo_area_enem = NULL;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_peso_area_enem_grupo_curso",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropCheckConstraint(
                name: "ck_curso_grupo_area_enem",
                schema: "configuracao",
                table: "curso");

            migrationBuilder.DropColumn(
                name: "grupo_curso_rotulo",
                schema: "configuracao",
                table: "peso_area_enem");

            migrationBuilder.DropColumn(
                name: "grupo_area_enem_rotulo",
                schema: "configuracao",
                table: "curso");

            migrationBuilder.AlterColumn<string>(
                name: "grupo_curso",
                schema: "configuracao",
                table: "peso_area_enem",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldComment: "Código do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), sem abreviação e sem acento, restrito aos quatro grupos pelo CHECK ck_peso_area_enem_grupo_curso; com a resolução, forma a chave de negócio da linha. O rótulo fica em grupo_curso_rotulo.");

            migrationBuilder.AlterColumn<string>(
                name: "grupo_area_enem",
                schema: "configuracao",
                table: "curso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true,
                oldComment: "Código do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), sem abreviação e sem acento, restrito aos quatro grupos pelo CHECK ck_curso_grupo_area_enem. O rótulo fica em grupo_area_enem_rotulo. Nulo quando o curso não declara grupo.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_peso_area_enem_grupo_curso",
                schema: "configuracao",
                table: "peso_area_enem",
                sql: "grupo_curso IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_curso_grupo_area_enem",
                schema: "configuracao",
                table: "curso",
                sql: "grupo_area_enem IS NULL OR grupo_area_enem IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");
        }
    }
}
