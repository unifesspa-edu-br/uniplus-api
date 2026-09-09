using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIndiceUnicidadeDiaNaoUtil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defesa de última linha contra duplicata sob concorrência real (RN05,
            // api#1458): a partir da introdução de IncluirDiaNaoUtil, o dataset deixa
            // de ser escrito uma única vez (Criar) e passa a admitir inclusões
            // concorrentes — a checagem em memória de CalendarioDiasUteis só compara
            // contra o que já está carregado no agregado no momento da leitura.
            // NULLS NOT DISTINCT (sintaxe suportada desde PostgreSQL 15, validada
            // manualmente contra postgres:18-alpine) trata NULL = NULL na chave de
            // negócio — sem isso, duas datas NACIONAL na mesma data (ambas com
            // municipio_ibge/uf nulos) não colidiriam, ao contrário da igualdade
            // estrutural de tupla que o HashSet do domínio usa. Índice comum (não
            // expressável como constraint de coluna), por isso vive só aqui e no
            // ModelSnapshot — mesmo padrão da exclusion constraint de Vigente em
            // 20260802202733_CriaCalendarioDiasUteis. Sem CONCURRENTLY: roda dentro da
            // transação padrão da migration, aceitável sem volume em produção.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_dia_nao_util_unicidade
                ON configuracao.dia_nao_util (calendario_dias_uteis_id, data, abrangencia, municipio_ibge, uf)
                NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX configuracao.ix_dia_nao_util_unicidade;");
        }
    }
}
