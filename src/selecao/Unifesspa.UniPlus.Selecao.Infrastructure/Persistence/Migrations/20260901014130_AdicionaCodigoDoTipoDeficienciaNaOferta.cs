using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCodigoDoTipoDeficienciaNaOferta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A coluna nasce anulável para o backfill acontecer antes de a
            // obrigatoriedade valer.
            migrationBuilder.AddColumn<string>(
                name: "tipo_deficiencia_codigo",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // O código é derivável do que já está gravado: o snapshot guarda o
            // identificador de origem, e o cadastro preserva suas linhas sob exclusão
            // lógica — então a linha de origem continua lá, com o código, mesmo que o
            // tipo tenha sido removido depois de configurado. Descartar a configuração
            // do operador quando ela pode ser reconstruída seria perda evitável.
            //
            // O guard não é zelo excessivo: cada módulo migra o próprio schema, e nada
            // garante o estado do de Configuração quando esta migration roda. Num banco
            // onde Seleção migra primeiro, a tabela pode não existir; num que esteja numa
            // revisão anterior à que introduziu o código, a tabela existe mas a coluna
            // não — e referenciar qualquer um dos dois diretamente abortaria a migração,
            // num caso por `undefined_table` e no outro por `undefined_column`.
            //
            // Por isso a checagem é pela COLUNA, que cobre os dois casos: sem ela não há
            // código a copiar, e sem a tabela não há sequer oferta a preservar.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                         WHERE table_schema = 'configuracao'
                           AND table_name = 'tipo_deficiencia'
                           AND column_name = 'codigo'
                    ) THEN
                        UPDATE selecao.ofertas_tipo_deficiencia AS oferta
                           SET tipo_deficiencia_codigo = cadastro.codigo
                          FROM configuracao.tipo_deficiencia AS cadastro
                         WHERE cadastro.id = oferta.tipo_deficiencia_origem_id;
                    END IF;
                END $$;
                """);

            // Resíduo: linha cuja origem não existe mais nem sob exclusão lógica. Não há
            // de onde tirar o código, e mantê-la com valor inventado deixaria um snapshot
            // que nenhuma regra casaria.
            //
            // Há um caso conhecido em que isso alcança tudo: um banco que ainda não tinha
            // aplicado a migration de Configuração que introduziu o código do tipo de
            // deficiência, porque ela apaga fisicamente o cadastro antes de criar a
            // coluna. Como Configuração migra antes de Seleção, o backfill acima encontra
            // o cadastro já vazio. A perda, nesse caso, é anterior a esta migration — a
            // oferta já apontava para origem inexistente desde aquele apagamento —, e o
            // que sobrava era o nome sem identidade que o resolvesse.
            migrationBuilder.Sql(
                """
                DELETE FROM selecao.ofertas_tipo_deficiencia
                 WHERE tipo_deficiencia_codigo IS NULL;
                """);

            // Agora sim obrigatória, e sem defaultValue: um default de string vazia
            // ficaria permanente no schema, disponível para gravar snapshot inválido em
            // qualquer insert futuro que omitisse a coluna.
            migrationBuilder.AlterColumn<string>(
                name: "tipo_deficiencia_codigo",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// A reversão devolve o schema ao estado anterior. As linhas preservadas pelo
        /// backfill continuam lá; só não voltam as que o <c>Up</c> removeu por não ter
        /// origem viva de onde derivar o código.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_deficiencia_codigo",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia");
        }
    }
}
