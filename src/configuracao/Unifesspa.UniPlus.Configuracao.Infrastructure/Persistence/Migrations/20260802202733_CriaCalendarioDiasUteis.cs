using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CriaCalendarioDiasUteis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendario_dias_uteis",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_calendario_dias_uteis", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dia_nao_util",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    calendario_dias_uteis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    municipio_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dia_nao_util", x => x.id);
                    table.CheckConstraint("ck_dia_nao_util_municipio_coerente", "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL)");
                    table.CheckConstraint("ck_dia_nao_util_uf_coerente", "(abrangencia = 'ESTADUAL') = (uf IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_dia_nao_util_calendarios_dias_uteis_calendario_dias_uteis_id",
                        column: x => x.calendario_dias_uteis_id,
                        principalSchema: "configuracao",
                        principalTable: "calendario_dias_uteis",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendario_dias_uteis_versao_dataset",
                schema: "configuracao",
                table: "calendario_dias_uteis",
                column: "versao_dataset",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_dia_nao_util_calendario_data",
                schema: "configuracao",
                table: "dia_nao_util",
                columns: new[] { "calendario_dias_uteis_id", "data" });

            // Pré-requisito da exclusion constraint abaixo: sem btree_gist o operador `=`
            // de boolean não tem classe GIST. `calendario_dias_uteis` compartilha o mesmo
            // banco físico do schema `publicacoes` (só schemas isolam os módulos, não
            // bancos — ver 20260710021244_AddTipoAtoPublicado, que já criou a extensão),
            // mas IF NOT EXISTS mantém esta migration autossuficiente mesmo rodando
            // isolada (ex.: containers efêmeros de teste de integração que só aplicam o
            // DbContext de Configuracao).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // No máximo um dataset vivo é vigente. Um índice único comum é verificado
            // por-statement: MarcarVigenteCalendarioDiasUteisCommandHandler desmarca o
            // vigente anterior e marca o novo na mesma SaveChangesAsync, e o EF Core não
            // garante a ordem entre os dois UPDATEs — com um índice não-deferível, a
            // ordem inversa colide (23505) mesmo a transação terminando num estado final
            // válido. DEFERRABLE INITIALLY DEFERRED adia a checagem para o COMMIT. Mesmo
            // padrão de ex_nos_exigencia_irmaos_ordem (NoExigenciaConfiguration.cs) e
            // ex_tipo_ato_publicado_codigo_vigencia (TipoAtoPublicadoConfiguration.cs) —
            // EXCLUDE com predicado parcial não é modelável pelo Fluent API do EF Core,
            // então vive só aqui e no ModelSnapshot (não em CalendarioDiasUteisConfiguration.cs).
            migrationBuilder.Sql(
                """
                ALTER TABLE configuracao.calendario_dias_uteis
                ADD CONSTRAINT ex_calendario_dias_uteis_vigente_unico
                EXCLUDE USING gist (
                    vigente WITH =
                ) WHERE (vigente = true AND is_deleted = false)
                DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dia_nao_util",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "calendario_dias_uteis",
                schema: "configuracao");
        }
    }
}
