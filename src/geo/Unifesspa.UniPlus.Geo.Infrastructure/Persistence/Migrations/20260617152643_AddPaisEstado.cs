using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaisEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ponto_referencia_sonda");

            migrationBuilder.CreateTable(
                name: "pais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sigla_iso = table.Column<string>(type: "text", nullable: false),
                    sigla = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    codigo_bcb = table.Column<string>(type: "text", nullable: true),
                    codigo_rfb = table.Column<string>(type: "text", nullable: true),
                    codigo_sped = table.Column<string>(type: "text", nullable: true),
                    codigo_siscomex = table.Column<string>(type: "text", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "estado",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pais_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uf = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    nome_normalizado = table.Column<string>(type: "text", nullable: true),
                    regiao = table.Column<string>(type: "text", nullable: true),
                    capital = table.Column<string>(type: "text", nullable: true),
                    codigo_ibge = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    coordenada = table.Column<Point>(type: "geography (Point, 4326)", nullable: true),
                    cep_inicial = table.Column<string>(type: "text", nullable: true),
                    cep_final = table.Column<string>(type: "text", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estado", x => x.id);
                    table.ForeignKey(
                        name: "fk_estado_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "pais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estado_faixa_cep",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cep_inicial = table.Column<string>(type: "text", nullable: false),
                    cep_final = table.Column<string>(type: "text", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estado_faixa_cep", x => x.id);
                    table.ForeignKey(
                        name: "fk_estado_faixa_cep_estado_estado_id",
                        column: x => x.estado_id,
                        principalTable: "estado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estado_indicador",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gentilico = table.Column<string>(type: "text", nullable: true),
                    governador = table.Column<string>(type: "text", nullable: true),
                    area_km2 = table.Column<decimal>(type: "numeric", nullable: true),
                    populacao_residente_2022 = table.Column<int>(type: "integer", nullable: true),
                    densidade_demografica = table.Column<decimal>(type: "numeric", nullable: true),
                    matriculas_ensino_fundamental_2023 = table.Column<int>(type: "integer", nullable: true),
                    idh = table.Column<decimal>(type: "numeric", nullable: true),
                    receitas_brutas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    despesas_brutas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    rendimento_mensal_per_capita = table.Column<int>(type: "integer", nullable: true),
                    total_veiculos_2023 = table.Column<int>(type: "integer", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estado_indicador", x => x.id);
                    table.ForeignKey(
                        name: "fk_estado_indicador_estado_estado_id",
                        column: x => x.estado_id,
                        principalTable: "estado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_estado_coordenada",
                table: "estado",
                column: "coordenada")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_estado_pais_id",
                table: "estado",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_estado_uf",
                table: "estado",
                column: "uf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_estado_versao_dataset",
                table: "estado",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_estado_faixa_cep_natural",
                table: "estado_faixa_cep",
                columns: new[] { "estado_id", "cep_inicial", "cep_final" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_estado_faixa_cep_versao_dataset",
                table: "estado_faixa_cep",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_estado_indicador_estado_id",
                table: "estado_indicador",
                column: "estado_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_estado_indicador_versao_dataset",
                table: "estado_indicador",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_pais_sigla_iso",
                table: "pais",
                column: "sigla_iso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pais_versao_dataset",
                table: "pais",
                column: "versao_dataset");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estado_faixa_cep");

            migrationBuilder.DropTable(
                name: "estado_indicador");

            migrationBuilder.DropTable(
                name: "estado");

            migrationBuilder.DropTable(
                name: "pais");

            migrationBuilder.CreateTable(
                name: "ponto_referencia_sonda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coordenada = table.Column<Point>(type: "geography (Point, 4326)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ponto_referencia_sonda", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ponto_referencia_sonda_coordenada",
                table: "ponto_referencia_sonda",
                column: "coordenada")
                .Annotation("Npgsql:IndexMethod", "gist");
        }
    }
}
