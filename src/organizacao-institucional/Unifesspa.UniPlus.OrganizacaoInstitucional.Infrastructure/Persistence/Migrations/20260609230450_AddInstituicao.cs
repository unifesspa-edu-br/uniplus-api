using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instituicao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    organizacao_academica = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    categoria_administrativa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mantenedora = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    codigo_mantenedora_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    situacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ato_credenciamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ato_recredenciamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    conceito_institucional = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    igc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    endereco_sede = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    municipio_sede = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    unidade_raiz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registro_vivo_sentinela = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("pk_instituicao", x => x.id);
                    table.CheckConstraint("ck_instituicao_singleton_sentinela", "registro_vivo_sentinela = true");
                    table.ForeignKey(
                        name: "fk_instituicao_unidades_unidade_raiz_id",
                        column: x => x.unidade_raiz_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_instituicao_singleton_vivo",
                table: "instituicao",
                column: "registro_vivo_sentinela",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_instituicao_unidade_raiz_id",
                table: "instituicao",
                column: "unidade_raiz_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Dropar a tabela aqui induziria um
            // `database update <baseline>` a destruir instituicao em staging/prod
            // sem audit trail — caminho proibido.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
