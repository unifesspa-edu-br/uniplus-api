using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "cidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uf = table.Column<string>(type: "text", nullable: false),
                    codigo_ibge = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    nome_normalizado = table.Column<string>(type: "text", nullable: true),
                    ddd = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    coordenada = table.Column<Point>(type: "geography (Point, 4326)", nullable: true),
                    mesorregiao_codigo = table.Column<string>(type: "text", nullable: true),
                    mesorregiao_nome = table.Column<string>(type: "text", nullable: true),
                    microrregiao_codigo = table.Column<string>(type: "text", nullable: true),
                    microrregiao_nome = table.Column<string>(type: "text", nullable: true),
                    regiao_intermediaria_codigo = table.Column<string>(type: "text", nullable: true),
                    regiao_intermediaria_nome = table.Column<string>(type: "text", nullable: true),
                    regiao_imediata_codigo = table.Column<string>(type: "text", nullable: true),
                    regiao_imediata_nome = table.Column<string>(type: "text", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cidade", x => x.id);
                    table.ForeignKey(
                        name: "fk_cidade_estados_estado_id",
                        column: x => x.estado_id,
                        principalTable: "estado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cidade_faixa_cep",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cep_inicial = table.Column<string>(type: "text", nullable: false),
                    cep_final = table.Column<string>(type: "text", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cidade_faixa_cep", x => x.id);
                    table.ForeignKey(
                        name: "fk_cidade_faixa_cep_cidade_cidade_id",
                        column: x => x.cidade_id,
                        principalTable: "cidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cidade_indicador",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gentilico = table.Column<string>(type: "text", nullable: true),
                    prefeito = table.Column<string>(type: "text", nullable: true),
                    area_km2 = table.Column<decimal>(type: "numeric", nullable: true),
                    populacao_residente = table.Column<int>(type: "integer", nullable: true),
                    densidade_demografica = table.Column<decimal>(type: "numeric", nullable: true),
                    escolarizacao_6_a_14 = table.Column<decimal>(type: "numeric", nullable: true),
                    idh = table.Column<decimal>(type: "numeric", nullable: true),
                    mortalidade_infantil = table.Column<decimal>(type: "numeric", nullable: true),
                    receitas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    despesas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    pib_per_capita = table.Column<decimal>(type: "numeric", nullable: true),
                    aniversario = table.Column<string>(type: "text", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cidade_indicador", x => x.id);
                    table.ForeignKey(
                        name: "fk_cidade_indicador_cidade_cidade_id",
                        column: x => x.cidade_id,
                        principalTable: "cidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cidade_codigo_ibge",
                table: "cidade",
                column: "codigo_ibge",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cidade_coordenada",
                table: "cidade",
                column: "coordenada")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_cidade_estado_id",
                table: "cidade",
                column: "estado_id");

            migrationBuilder.CreateIndex(
                name: "ix_cidade_nome_normalizado_trgm",
                table: "cidade",
                column: "nome_normalizado")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_cidade_versao_dataset",
                table: "cidade",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_cidade_faixa_cep_natural",
                table: "cidade_faixa_cep",
                columns: new[] { "cidade_id", "cep_inicial", "cep_final" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cidade_faixa_cep_versao_dataset",
                table: "cidade_faixa_cep",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_cidade_indicador_cidade_id",
                table: "cidade_indicador",
                column: "cidade_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cidade_indicador_versao_dataset",
                table: "cidade_indicador",
                column: "versao_dataset");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cidade_faixa_cep");

            migrationBuilder.DropTable(
                name: "cidade_indicador");

            migrationBuilder.DropTable(
                name: "cidade");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
