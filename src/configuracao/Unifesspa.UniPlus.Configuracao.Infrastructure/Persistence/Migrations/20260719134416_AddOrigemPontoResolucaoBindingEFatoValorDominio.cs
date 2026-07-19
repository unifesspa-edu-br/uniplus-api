using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrigemPontoResolucaoBindingEFatoValorDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_natureza",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.RenameColumn(
                name: "natureza",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                newName: "origem");

            migrationBuilder.AlterColumn<string>(
                name: "descricao",
                schema: "configuracao",
                table: "tipo_deficiencia",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "permanente",
                schema: "configuracao",
                table: "tipo_deficiencia",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "binding",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ponto_resolucao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "fato_valor_dominio",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fato_candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fato_valor_dominio", x => x.id);
                    table.ForeignKey(
                        name: "fk_fato_valor_dominio_rol_de_fatos_candidato_fato_candidato_id",
                        column: x => x.fato_candidato_id,
                        principalSchema: "configuracao",
                        principalTable: "rol_de_fatos_candidato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000001"), true, "BRANCA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Autodeclaração de cor/raça branca.", new Guid("fa700000-0000-7000-8000-000000000001"), 0, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000002"), true, "PRETA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Autodeclaração de cor/raça preta, conforme Lei 12.711/2012 e resoluções da Unifesspa sobre heteroidentificação.", new Guid("fa700000-0000-7000-8000-000000000001"), 1, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000003"), true, "PARDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Autodeclaração de cor/raça parda, conforme Lei 12.711/2012 e resoluções da Unifesspa sobre heteroidentificação.", new Guid("fa700000-0000-7000-8000-000000000001"), 2, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000004"), true, "AMARELA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Autodeclaração de cor/raça amarela (ascendência asiática).", new Guid("fa700000-0000-7000-8000-000000000001"), 3, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000005"), true, "INDIGENA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Autodeclaração de povo indígena.", new Guid("fa700000-0000-7000-8000-000000000001"), 4, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000006"), true, "NAO_INFORMADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Candidato optou por não informar cor ou raça.", new Guid("fa700000-0000-7000-8000-000000000001"), 5, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000007"), true, "FEMININO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sexo feminino.", new Guid("fa700000-0000-7000-8000-000000000007"), 0, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000008"), true, "MASCULINO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sexo masculino.", new Guid("fa700000-0000-7000-8000-000000000007"), 1, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000009"), true, "INTERSEXO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pessoa intersexo — variação natural das características sexuais.", new Guid("fa700000-0000-7000-8000-000000000007"), 2, null }
                });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                columns: new[] { "binding", "origem", "ponto_resolucao", "valores_dominio" },
                values: new object[] { "CAMPO_INSCRICAO:COR_RACA", "DECLARADO", "INSCRICAO", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                columns: new[] { "binding", "origem", "ponto_resolucao" },
                values: new object[] { "CAMPO_INSCRICAO:QUILOMBOLA", "DECLARADO", "INSCRICAO" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                columns: new[] { "binding", "origem", "ponto_resolucao" },
                values: new object[] { "CAMPO_INSCRICAO:PCD", "DECLARADO", "INSCRICAO" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                columns: new[] { "binding", "origem", "ponto_resolucao" },
                values: new object[] { "CAMPO_INSCRICAO:EGRESSO_ESCOLA_PUBLICA", "DECLARADO", "INSCRICAO" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                columns: new[] { "binding", "origem", "ponto_resolucao" },
                values: new object[] { "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA", "DERIVADO", "INSCRICAO" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                columns: new[] { "binding", "origem", "ponto_resolucao" },
                values: new object[] { "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", "DERIVADO", "INSCRICAO" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                columns: new[] { "binding", "origem", "ponto_resolucao", "valores_dominio" },
                values: new object[] { "CAMPO_INSCRICAO:SEXO", "DECLARADO", "INSCRICAO", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                columns: new[] { "binding", "origem", "ponto_resolucao" },
                values: new object[] { "CAMPO_INSCRICAO:MODALIDADE", "DECLARADO", "INSCRICAO" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                columns: new[] { "binding", "origem", "ponto_resolucao" },
                values: new object[] { "CAMPO_INSCRICAO:CONDICAO_ATENDIMENTO", "DECLARADO", "INSCRICAO" });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "binding", "cardinalidade", "codigo", "created_at", "descricao", "dominio", "nome", "origem", "ponto_resolucao", "updated_at", "valores_dominio" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000010"), "CAMPO_INSCRICAO:NACIONALIDADE", "ESCALAR", "NACIONALIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "CATEGORICO", "Nacionalidade", "DECLARADO", "INSCRICAO", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000011"), "CAMPO_INSCRICAO:TIPO_DEFICIENCIA", "ESCALAR", "TIPO_DEFICIENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "CATEGORICO", "Tipo de deficiência", "DECLARADO", "INSCRICAO", null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000010"), true, "NATO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Brasileiro nato, nascido no Brasil ou nas condições previstas pela Constituição.", new Guid("fa700000-0000-7000-8000-000000000010"), 0, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000011"), true, "NATURALIZADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Brasileiro naturalizado, conforme processo de naturalização reconhecido.", new Guid("fa700000-0000-7000-8000-000000000010"), 1, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000012"), true, "ESTRANGEIRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cidadão estrangeiro, não brasileiro.", new Guid("fa700000-0000-7000-8000-000000000010"), 2, null }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_origem",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "origem IN ('DERIVADO', 'DECLARADO', 'INTEGRACAO')");

            migrationBuilder.CreateIndex(
                name: "ux_fato_valor_dominio_fato_codigo",
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "fato_candidato_id", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fato_valor_dominio",
                schema: "configuracao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_origem",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"));

            migrationBuilder.DropColumn(
                name: "permanente",
                schema: "configuracao",
                table: "tipo_deficiencia");

            migrationBuilder.DropColumn(
                name: "binding",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "ponto_resolucao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.RenameColumn(
                name: "origem",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                newName: "natureza");

            migrationBuilder.AlterColumn<string>(
                name: "descricao",
                schema: "configuracao",
                table: "tipo_deficiencia",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                columns: new[] { "natureza", "valores_dominio" },
                values: new object[] { "BRUTO_INFORMADO", "[\"BRANCA\",\"PRETA\",\"PARDA\",\"AMARELA\",\"INDIGENA\",\"NAO_INFORMADO\"]" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                column: "natureza",
                value: "BRUTO_INFORMADO");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                column: "natureza",
                value: "BRUTO_INFORMADO");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                column: "natureza",
                value: "BRUTO_INFORMADO");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                column: "natureza",
                value: "BRUTO_INFORMADO");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                column: "natureza",
                value: "BRUTO_INFORMADO");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                columns: new[] { "natureza", "valores_dominio" },
                values: new object[] { "BRUTO_INFORMADO", "[\"FEMININO\",\"MASCULINO\",\"INTERSEXO\"]" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                column: "natureza",
                value: "BRUTO_INFORMADO");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                column: "natureza",
                value: "BRUTO_INFORMADO");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_natureza",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "natureza IN ('BRUTO_INFORMADO', 'DE_VONTADE', 'DERIVADO')");
        }
    }
}
