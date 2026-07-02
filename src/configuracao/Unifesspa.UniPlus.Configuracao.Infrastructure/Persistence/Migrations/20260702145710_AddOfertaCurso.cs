using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfertaCurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "oferta_curso",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_oferta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_oft_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_oft_sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unidade_oft_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    unidade_oft_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    programa_de_oferta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    formato_pedagogico = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    turno = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    e_mec_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    codigo_sga = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    vagas_anuais_autorizadas = table.Column<int>(type: "integer", nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ato_autorizacao_mec = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("pk_oferta_curso", x => x.id);
                    table.CheckConstraint("ck_oferta_curso_base_legal_programa", "programa_de_oferta = 'REGULAR' OR base_legal IS NOT NULL");
                    table.CheckConstraint("ck_oferta_curso_formato_pedagogico", "formato_pedagogico IN ('PRESENCIAL', 'SEMIPRESENCIAL', 'EAD')");
                    table.CheckConstraint("ck_oferta_curso_programa_de_oferta", "programa_de_oferta IN ('REGULAR', 'FORMA_PARA', 'PARFOR', 'PRONERA', 'PEPETI', 'CONVENIO_OUTRO', 'OUTRO')");
                    table.CheckConstraint("ck_oferta_curso_turno", "turno IS NULL OR turno IN ('MATUTINO', 'VESPERTINO', 'NOTURNO', 'INTEGRAL')");
                    table.CheckConstraint("ck_oferta_curso_vagas_anuais_autorizadas", "vagas_anuais_autorizadas IS NULL OR vagas_anuais_autorizadas >= 0");
                    table.ForeignKey(
                        name: "fk_oferta_curso_curso_curso_id",
                        column: x => x.curso_id,
                        principalSchema: "configuracao",
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_oferta_curso_local_oferta_local_oferta_id",
                        column: x => x.local_oferta_id,
                        principalSchema: "configuracao",
                        principalTable: "local_oferta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_oferta_curso_curso_id",
                schema: "configuracao",
                table: "oferta_curso",
                column: "curso_id");

            migrationBuilder.CreateIndex(
                name: "ix_oferta_curso_local_oferta_id",
                schema: "configuracao",
                table: "oferta_curso",
                column: "local_oferta_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "oferta_curso",
                schema: "configuracao");
        }
    }
}
