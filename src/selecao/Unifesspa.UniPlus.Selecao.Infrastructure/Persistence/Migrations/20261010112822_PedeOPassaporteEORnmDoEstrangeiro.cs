using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedeOPassaporteEORnmDoEstrangeiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os dois itens do documento do estrangeiro ocupam, na seção dos dados básicos, a ordem
            // e a exibição do passaporte e do RNM, que os substituem.
            migrationBuilder.Sql("""
                UPDATE selecao.fatos_coletados
                SET fato_codigo = 'PASSAPORTE_NUMERO', rotulo = 'Número do passaporte', tipo_renderizacao = 5,
                    formato = 'LIVRE', classificacao_protecao = 'IDENTIFICADOR'
                WHERE etapa_codigo = 'DADOS_BASICOS' AND fato_codigo = 'DOCUMENTO_ESTRANGEIRO_TIPO';
                """);
            migrationBuilder.Sql("""
                UPDATE selecao.fatos_coletados
                SET fato_codigo = 'RNM_NUMERO', rotulo = 'Número do Registro Nacional Migratório (RNM)'
                WHERE etapa_codigo = 'DADOS_BASICOS' AND fato_codigo = 'DOCUMENTO_ESTRANGEIRO_NUMERO';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O documento do estrangeiro à escolha não é restaurado.
        }
    }
}
