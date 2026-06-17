using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLogradouro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cep_grande_usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cep = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    nome_normalizado = table.Column<string>(type: "text", nullable: true),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cep_grande_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "logradouro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cep = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: true),
                    nome = table.Column<string>(type: "text", nullable: false),
                    nome_completo = table.Column<string>(type: "text", nullable: true),
                    nome_normalizado = table.Column<string>(type: "text", nullable: false),
                    cidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    distrito_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bairro_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uf = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    coordenada = table.Column<Point>(type: "geography (Point, 4326)", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_logradouro", x => x.id);
                    table.ForeignKey(
                        name: "fk_logradouro_bairro_bairro_id",
                        column: x => x.bairro_id,
                        principalTable: "bairro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_logradouro_cidade_cidade_id",
                        column: x => x.cidade_id,
                        principalTable: "cidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_logradouro_distrito_distrito_id",
                        column: x => x.distrito_id,
                        principalTable: "distrito",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "logradouro_complemento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cep = table.Column<string>(type: "text", nullable: false),
                    complemento = table.Column<string>(type: "text", nullable: false),
                    complemento_normalizado = table.Column<string>(type: "text", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_logradouro_complemento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cep_grande_usuario_cep",
                table: "cep_grande_usuario",
                column: "cep",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cep_grande_usuario_versao_dataset",
                table: "cep_grande_usuario",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_logradouro_bairro_id",
                table: "logradouro",
                column: "bairro_id");

            migrationBuilder.CreateIndex(
                name: "ix_logradouro_cidade_id",
                table: "logradouro",
                column: "cidade_id");

            migrationBuilder.CreateIndex(
                name: "ix_logradouro_coordenada",
                table: "logradouro",
                column: "coordenada")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_logradouro_distrito_id",
                table: "logradouro",
                column: "distrito_id");

            migrationBuilder.CreateIndex(
                name: "ix_logradouro_natural",
                table: "logradouro",
                columns: new[] { "cep", "nome_normalizado", "cidade_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_logradouro_nome_trgm",
                table: "logradouro",
                column: "nome_normalizado")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_logradouro_versao_dataset",
                table: "logradouro",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ix_logradouro_complemento_versao_dataset",
                table: "logradouro_complemento",
                column: "versao_dataset");

            migrationBuilder.CreateIndex(
                name: "ux_logradouro_complemento_cep_complemento",
                table: "logradouro_complemento",
                columns: new[] { "cep", "complemento_normalizado" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cep_grande_usuario");

            migrationBuilder.DropTable(
                name: "logradouro");

            migrationBuilder.DropTable(
                name: "logradouro_complemento");
        }
    }
}
