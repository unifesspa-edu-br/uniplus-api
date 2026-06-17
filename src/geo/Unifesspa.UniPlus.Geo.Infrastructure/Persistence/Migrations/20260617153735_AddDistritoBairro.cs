using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDistritoBairro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bairro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uf = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    nome_normalizado = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    coordenada = table.Column<Point>(type: "geography (Point, 4326)", nullable: true),
                    id_origem_dne = table.Column<string>(type: "text", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bairro", x => x.id);
                    table.ForeignKey(
                        name: "fk_bairro_cidades_cidade_id",
                        column: x => x.cidade_id,
                        principalTable: "cidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "distrito",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uf = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    nome_normalizado = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    coordenada = table.Column<Point>(type: "geography (Point, 4326)", nullable: true),
                    id_origem_dne = table.Column<string>(type: "text", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_distrito", x => x.id);
                    table.ForeignKey(
                        name: "fk_distrito_cidade_cidade_id",
                        column: x => x.cidade_id,
                        principalTable: "cidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bairro_faixa_cep",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bairro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cep_inicial = table.Column<string>(type: "text", nullable: false),
                    cep_final = table.Column<string>(type: "text", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bairro_faixa_cep", x => x.id);
                    table.ForeignKey(
                        name: "fk_bairro_faixa_cep_bairro_bairro_id",
                        column: x => x.bairro_id,
                        principalTable: "bairro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "distrito_faixa_cep",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    distrito_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cep_inicial = table.Column<string>(type: "text", nullable: false),
                    cep_final = table.Column<string>(type: "text", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_distrito_faixa_cep", x => x.id);
                    table.ForeignKey(
                        name: "fk_distrito_faixa_cep_distrito_distrito_id",
                        column: x => x.distrito_id,
                        principalTable: "distrito",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bairro_cidade_nome",
                table: "bairro",
                columns: new[] { "cidade_id", "nome_normalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bairro_coordenada",
                table: "bairro",
                column: "coordenada")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_bairro_versao_dataset",
                table: "bairro",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_bairro_faixa_cep_natural",
                table: "bairro_faixa_cep",
                columns: new[] { "bairro_id", "cep_inicial", "cep_final" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bairro_faixa_cep_versao_dataset",
                table: "bairro_faixa_cep",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_distrito_cidade_nome",
                table: "distrito",
                columns: new[] { "cidade_id", "nome_normalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_distrito_coordenada",
                table: "distrito",
                column: "coordenada")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_distrito_versao_dataset",
                table: "distrito",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_distrito_faixa_cep_natural",
                table: "distrito_faixa_cep",
                columns: new[] { "distrito_id", "cep_inicial", "cep_final" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_distrito_faixa_cep_versao_dataset",
                table: "distrito_faixa_cep",
                column: "versao_dataset");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bairro_faixa_cep");

            migrationBuilder.DropTable(
                name: "distrito_faixa_cep");

            migrationBuilder.DropTable(
                name: "bairro");

            migrationBuilder.DropTable(
                name: "distrito");
        }
    }
}
