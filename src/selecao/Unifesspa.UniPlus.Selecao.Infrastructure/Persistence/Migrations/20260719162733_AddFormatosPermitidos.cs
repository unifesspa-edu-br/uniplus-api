using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormatosPermitidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Passo 1 — colunas novas, AMBAS nullable nesta etapa: nenhuma linha existente
            // fica inconsistente enquanto o backfill (passo 2) ainda não rodou.
            migrationBuilder.AddColumn<bool>(
                name: "formatos_permitidos_qualquer",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "formatos_permitidos_lista",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "jsonb",
                nullable: true);

            // Passo 2 — backfill fiel ao significado antigo de `formato_permitido`:
            // NULL ("sem restrição") vira QUALQUER; um formato preenchido vira uma lista de
            // um item, sem teto por formato (o teto por-formato não existia antes da Story
            // #918 — o teto GLOBAL antigo permanece intocado em `tamanho_maximo_bytes`).
            migrationBuilder.Sql(
                """
                UPDATE selecao.documentos_exigidos
                SET formatos_permitidos_qualquer = true,
                    formatos_permitidos_lista = NULL
                WHERE formato_permitido IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE selecao.documentos_exigidos
                SET formatos_permitidos_qualquer = false,
                    formatos_permitidos_lista = jsonb_build_array(
                        jsonb_build_object('formato', formato_permitido, 'tamanhoMaximoBytesMax', NULL))
                WHERE formato_permitido IS NOT NULL;
                """);

            // Passo 3 — trava a coluna NOT NULL e o CHECK de coerência, agora que toda
            // linha existente já tem o par (qualquer, lista) preenchido pelo backfill.
            migrationBuilder.AlterColumn<bool>(
                name: "formatos_permitidos_qualquer",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_documentos_exigidos_formatos_permitidos_coerente",
                schema: "selecao",
                table: "documentos_exigidos",
                sql: "(formatos_permitidos_qualquer = true AND formatos_permitidos_lista IS NULL) OR (formatos_permitidos_qualquer = false AND formatos_permitidos_lista IS NOT NULL AND jsonb_array_length(formatos_permitidos_lista) > 0)");

            // Passo 4 — a coluna antiga só sai depois que toda linha já tem o par novo.
            migrationBuilder.DropColumn(
                name: "formato_permitido",
                schema: "selecao",
                table: "documentos_exigidos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "formato_permitido",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            // Reverte o backfill: QUALQUER volta a "sem restrição"; uma lista de exatamente
            // um item sem teto volta ao formato singular antigo. Uma lista com mais de um
            // item ou com teto por formato (dado que só existe depois desta migration) não
            // tem representação no formato antigo — fica NULL, mesma perda de informação que
            // qualquer Down de coluna estreitada.
            migrationBuilder.Sql(
                """
                UPDATE selecao.documentos_exigidos
                SET formato_permitido = formatos_permitidos_lista->0->>'formato'
                WHERE formatos_permitidos_qualquer = false
                  AND jsonb_array_length(formatos_permitidos_lista) = 1
                  AND formatos_permitidos_lista->0->'tamanhoMaximoBytesMax' = 'null'::jsonb;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_documentos_exigidos_formatos_permitidos_coerente",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "formatos_permitidos_lista",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "formatos_permitidos_qualquer",
                schema: "selecao",
                table: "documentos_exigidos");
        }
    }
}
