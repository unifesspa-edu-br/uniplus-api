using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampusELocalOferta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sigla = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    cidade_origem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cidade_display_atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cep = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "local_oferta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    campus_responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    cidade_origem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cidade_display_atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_local_oferta", x => x.id);
                    table.ForeignKey(
                        name: "fk_local_oferta_campus_campus_responsavel_id",
                        column: x => x.campus_responsavel_id,
                        principalTable: "campus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campus_cidade_codigo_ibge",
                table: "campus",
                column: "cidade_codigo_ibge");

            migrationBuilder.CreateIndex(
                name: "ix_campus_sigla_vivo",
                table: "campus",
                column: "sigla",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_local_oferta_campus_responsavel_id",
                table: "local_oferta",
                column: "campus_responsavel_id");

            migrationBuilder.CreateIndex(
                name: "ix_local_oferta_cidade_codigo_ibge",
                table: "local_oferta",
                column: "cidade_codigo_ibge");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "local_oferta");

            migrationBuilder.DropTable(
                name: "campus");
        }
    }
}
