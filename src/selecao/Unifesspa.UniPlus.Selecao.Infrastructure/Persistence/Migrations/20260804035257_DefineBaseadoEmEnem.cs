using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DefineBaseadoEmEnem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "baseado_em_enem",
                schema: "selecao",
                table: "configuracoes_classificacao",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "A classificação usa a estrutura de pontuação por área do ENEM — sinal explícito (Story #850) do qual ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA dependem, substituindo a ramificação por TipoProcesso.");

            // Backfill: preserva o comportamento anterior para configurações já persistidas.
            // Antes desta migration, a aceitação de ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA
            // dependia de ProcessoSeletivo.Tipo (SiSU=1, PSVR=4), não de um campo próprio da
            // configuração — sem este UPDATE, toda linha existente nasceria com
            // baseado_em_enem=false mesmo tendo essas regras persistidas, um estado que
            // ConfiguracaoClassificacao.Criar nunca mais aceitaria construir e que o decoder
            // do envelope (EnvelopeCodecV11.LerClassificacao) recusaria ao restaurar.
            migrationBuilder.Sql(
                """
                UPDATE selecao.configuracoes_classificacao AS c
                SET baseado_em_enem = TRUE
                FROM selecao.processos_seletivos AS p
                WHERE p.id = c.processo_seletivo_id
                  AND p.tipo IN (1, 4);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "baseado_em_enem",
                schema: "selecao",
                table: "configuracoes_classificacao");
        }
    }
}
