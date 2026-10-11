using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetiraAsDerivacoesQueAOfertaDefine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A modalidade do candidato e a opção pelas cotas da lei passam a sair das modalidades
            // ofertadas; a cópia gravada no processo deixaria duas fontes da mesma regra. As regras e
            // as condições saem em cascata.
            migrationBuilder.Sql(
                """
                DELETE FROM selecao.configuracoes_derivacao_fato
                WHERE codigo_fato IN ('MODALIDADE', 'OPTOU_PELAS_COTAS_DA_LEI');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não há volta: a regra que sai é a que a oferta passou a definir.
        }
    }
}
