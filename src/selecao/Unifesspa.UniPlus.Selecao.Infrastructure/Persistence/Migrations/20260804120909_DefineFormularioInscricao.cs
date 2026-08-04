using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DefineFormularioInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "formulario_termo_aceite_texto",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                comment: "Texto do termo de aceite do formulário de inscrição. Ausência = sem termo configurado.");

            migrationBuilder.AddColumn<string>(
                name: "formulario_titulo",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                comment: "Título do formulário de inscrição apresentado ao candidato. Ausência = sem título configurado.");

            migrationBuilder.AddColumn<bool>(
                name: "obrigatorio",
                schema: "selecao",
                table: "fatos_coletados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "rotulo",
                schema: "selecao",
                table: "fatos_coletados",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "tipo_renderizacao",
                schema: "selecao",
                table: "fatos_coletados",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: preserva Rotulo/TipoRenderizacao coerentes para fatos_coletados já
            // persistidos. Sem este UPDATE, toda linha existente nasceria com rotulo='' e
            // tipo_renderizacao=0 (TipoRenderizacao.Nenhuma) — um estado que FatoColetado.Criar
            // nunca mais aceitaria construir e que o decoder do envelope recusaria ao restaurar.
            // Os valores vêm do vocabulário fechado (configuracao.rol_de_fatos_candidato): nome
            // do fato como rótulo, domínio/cardinalidade mapeados para TipoRenderizacao pela
            // mesma regra de coerência aplicada na escrita (1=Numero, 2=Booleano,
            // 3=SelecaoUnica, 4=SelecaoMultipla).
            migrationBuilder.Sql(
                """
                UPDATE selecao.fatos_coletados
                SET rotulo = 'Cor ou raça', tipo_renderizacao = 3
                WHERE fato_codigo = 'COR_RACA' AND rotulo = '';

                UPDATE selecao.fatos_coletados
                SET rotulo = 'Renda familiar per capita igual ou inferior a um salário mínimo', tipo_renderizacao = 2
                WHERE fato_codigo = 'BAIXA_RENDA' AND rotulo = '';
                """);

            // Gate: o backfill acima só cobre os dois códigos observados no banco compartilhado
            // de desenvolvimento. Um ambiente com fatos_coletados de OUTRO código legado (ex.:
            // PCD, QUILOMBOLA) sairia daqui com rotulo='' e tipo_renderizacao=0
            // (TipoRenderizacao.Nenhuma) — um estado que FatoColetado.Criar nunca aceitaria
            // construir, e que TipoRenderizacao.ToCodigo() lança ao tentar serializar (a leitura
            // administrativa e a publicação do processo quebrariam em 500 para esse processo,
            // silenciosamente, só na primeira vez que alguém tentasse). Falhar a migration aqui —
            // em vez de deixar a linha inconsistente — obriga a estender o backfill acima para o
            // código adicional antes de aplicá-la naquele ambiente.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    restantes INTEGER;
                BEGIN
                    SELECT COUNT(*) INTO restantes FROM selecao.fatos_coletados WHERE rotulo = '';
                    IF restantes > 0 THEN
                        RAISE EXCEPTION 'Migration DefineFormularioInscricao: % linha(s) de fatos_coletados com fato_codigo fora do backfill conhecido (COR_RACA, BAIXA_RENDA) — estenda o backfill desta migration para cobrir os códigos adicionais antes de aplicá-la neste ambiente.', restantes;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "formulario_termo_aceite_texto",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "formulario_titulo",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "obrigatorio",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "rotulo",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "tipo_renderizacao",
                schema: "selecao",
                table: "fatos_coletados");
        }
    }
}
