using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CongelaQuadroDePesosPorAreaNaClassificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem preservação (não há produção): a classificação baseada em ENEM com cálculo
            // local passa a exigir a resolução de Pesos por Área e o quadro congelado dela, e
            // uma linha gravada antes desta migration não tem nenhum dos dois. Ela publicaria
            // um envelope que o próprio decodificador recusa ao reidratar. A classificação
            // volta a "não definida" — as regras de eliminação dela vão junto, em cascata; nada
            // mais referencia a linha — e o operador redefine o passo, agora com a resolução.
            migrationBuilder.Sql(
                """
                DELETE FROM selecao.configuracoes_classificacao
                WHERE baseado_em_enem
                  AND regra_calculo_codigo = 'FORMULA-MEDIA-PONDERADA';
                """);

            migrationBuilder.AddColumn<string>(
                name: "resolucao_peso_area_enem",
                schema: "selecao",
                table: "configuracoes_classificacao",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                comment: "Resolução de Pesos por Área declarada pela classificação baseada em ENEM com cálculo local; o vínculo com o cadastro é pelo valor, e o quadro fica congelado em grupos_peso_area_enem_congelados. Nulo nas demais classificações.");

            migrationBuilder.CreateTable(
                name: "grupos_peso_area_enem_congelados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) do grupo congelado."),
                    configuracao_classificacao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id da configuração de classificação dona do quadro (FK, cascade delete)."),
                    grupo_area_enem_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código do grupo de área do ENEM, sem abreviação e sem acento, copiado da resolução de Pesos por Área; casa a oferta com a linha do quadro."),
                    grupo_area_enem_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Rótulo do grupo de área do ENEM, copiado junto do código."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Dispositivo legal que fundamenta os pesos do grupo, copiado da resolução de Pesos por Área."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_peso_area_enem_congelados", x => x.id);
                    table.ForeignKey(
                        name: "fk_grupos_peso_area_enem_congelados_configuracoes_classificaca",
                        column: x => x.configuracao_classificacao_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_classificacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Quadro de pesos por área do ENEM congelado na classificação do processo seletivo: uma linha por grupo de área, copiada por valor da resolução de Pesos por Área declarada. Substituída por inteiro quando a classificação é redefinida.");

            migrationBuilder.CreateTable(
                name: "areas_peso_area_enem_congeladas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) da área congelada."),
                    grupo_peso_area_enem_congelado_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id do grupo congelado dono da área (FK, cascade delete)."),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código da área do ENEM, sem abreviação e sem acento, copiado da resolução de Pesos por Área."),
                    rotulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Rótulo oficial da área do ENEM, copiado junto do código."),
                    peso = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false, comment: "Peso da área na média do grupo, copiado da resolução de Pesos por Área."),
                    corte = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true, comment: "Nota mínima da área (0 a 1000), copiada da resolução de Pesos por Área; nulo quando a área não tem corte."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_areas_peso_area_enem_congeladas", x => x.id);
                    table.CheckConstraint("ck_areas_peso_area_enem_congeladas_corte", "corte IS NULL OR (corte >= 0 AND corte <= 1000)");
                    table.CheckConstraint("ck_areas_peso_area_enem_congeladas_peso", "peso >= 0");
                    table.ForeignKey(
                        name: "fk_areas_peso_area_enem_congeladas_grupos_peso_area_enem_conge",
                        column: x => x.grupo_peso_area_enem_congelado_id,
                        principalSchema: "selecao",
                        principalTable: "grupos_peso_area_enem_congelados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Peso e corte de cada área do ENEM num grupo do quadro de pesos por área congelado na classificação, copiados por valor da resolução de Pesos por Área declarada.");

            migrationBuilder.CreateIndex(
                name: "ix_areas_peso_area_enem_congeladas_grupo_peso_area_enem_congel",
                schema: "selecao",
                table: "areas_peso_area_enem_congeladas",
                columns: new[] { "grupo_peso_area_enem_congelado_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grupos_peso_area_enem_congelados_configuracao_classificacao",
                schema: "selecao",
                table: "grupos_peso_area_enem_congelados",
                column: "configuracao_classificacao_id");

            // Um grupo aparece uma vez por classificação. A chave combina a FK com uma coluna
            // do grupo congelado, que o modelo mapeia como tipo owned — combinação que o EF não
            // expressa num índice. Por isso o índice vive só aqui, e cai junto com a tabela no
            // Down.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_grupos_peso_area_enem_congelados_classificacao_grupo
                ON selecao.grupos_peso_area_enem_congelados (configuracao_classificacao_id, grupo_area_enem_codigo);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "areas_peso_area_enem_congeladas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "grupos_peso_area_enem_congelados",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "resolucao_peso_area_enem",
                schema: "selecao",
                table: "configuracoes_classificacao");
        }
    }
}
