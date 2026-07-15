using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFatoCandidato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rol_de_fatos_candidato",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    dominio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    natureza = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cardinalidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valores_dominio = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol_de_fatos_candidato", x => x.id);
                    table.CheckConstraint("ck_rol_de_fatos_candidato_cardinalidade", "cardinalidade IN ('ESCALAR', 'MULTIVALORADO')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_dominio", "dominio IN ('CATEGORICO', 'BOOLEANO', 'NUMERICO')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_natureza", "natureza IN ('BRUTO_INFORMADO', 'DE_VONTADE', 'DERIVADO')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_valores_dominio_coerente", "valores_dominio IS NULL OR (\n    dominio = 'CATEGORICO'\n    AND jsonb_typeof(valores_dominio) = 'array'\n    AND valores_dominio <> '[]'::jsonb\n    AND NOT (valores_dominio @? '$[*] ? (@.type() != \"string\" || @ like_regex \"^\\\\s*$\")')\n)");
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "cardinalidade", "codigo", "created_at", "descricao", "dominio", "natureza", "nome", "updated_at", "valores_dominio" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000001"), "ESCALAR", "COR_RACA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "CATEGORICO", "BRUTO_INFORMADO", "Cor ou raça", null, "[\"BRANCA\",\"PRETA\",\"PARDA\",\"AMARELA\",\"INDIGENA\",\"NAO_INFORMADO\"]" },
                    { new Guid("fa700000-0000-7000-8000-000000000002"), "ESCALAR", "QUILOMBOLA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "BRUTO_INFORMADO", "Quilombola", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000003"), "ESCALAR", "PCD", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "BRUTO_INFORMADO", "Pessoa com deficiência", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000004"), "ESCALAR", "EGRESSO_ESCOLA_PUBLICA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "BRUTO_INFORMADO", "Egresso de escola pública", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000005"), "ESCALAR", "RENDA_PER_CAPITA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "NUMERICO", "BRUTO_INFORMADO", "Renda familiar per capita", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000006"), "ESCALAR", "FAIXA_ETARIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "NUMERICO", "BRUTO_INFORMADO", "Faixa etária", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000007"), "ESCALAR", "SEXO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "CATEGORICO", "BRUTO_INFORMADO", "Sexo", null, "[\"FEMININO\",\"MASCULINO\",\"INTERSEXO\"]" },
                    { new Guid("fa700000-0000-7000-8000-000000000008"), "MULTIVALORADO", "MODALIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "CATEGORICO", "BRUTO_INFORMADO", "Modalidade de concorrência", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000009"), "MULTIVALORADO", "CONDICAO_ATENDIMENTO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "CATEGORICO", "BRUTO_INFORMADO", "Condição de atendimento especializado", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "ux_rol_de_fatos_candidato_codigo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                column: "codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rol_de_fatos_candidato",
                schema: "configuracao");
        }
    }
}
