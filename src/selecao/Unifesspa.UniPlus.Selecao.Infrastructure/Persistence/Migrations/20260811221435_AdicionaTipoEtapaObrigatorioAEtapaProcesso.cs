using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTipoEtapaObrigatorioAEtapaProcesso : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Sem produção em nenhum ambiente (issue #1071): as 3 colunas nascem <c>NOT NULL</c>
        /// diretamente, sem <c>defaultValue</c> sentinela e sem fase de transição — o enum
        /// órfão que existia antes nunca esteve vinculado a nenhuma linha de
        /// <c>etapas_processo</c>, então não há dado legado a preservar ou inferir por
        /// heurística. Um banco local/homologação com linhas pré-existentes de
        /// <c>etapas_processo</c> precisa ser recriado ao adotar esta migration.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_etapa_codigo",
                schema: "selecao",
                table: "etapas_processo",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "tipo_etapa_nome",
                schema: "selecao",
                table: "etapas_processo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_etapa_origem_id",
                schema: "selecao",
                table: "etapas_processo",
                type: "uuid",
                nullable: false,
                comment: "Id de origem do tipo de etapa em Configuração, sem FK cross-schema; congelado na definição.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_etapa_codigo",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "tipo_etapa_nome",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "tipo_etapa_origem_id",
                schema: "selecao",
                table: "etapas_processo");
        }
    }
}
