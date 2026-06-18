using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reconciliação de dados (#707): <c>nome_normalizado</c> passou a guardar o TEXTO
    /// COMPLETO sem acento (tipo + nome); antes guardava só o nome sem o tipo. Linhas
    /// gravadas pela versão anterior do ETL ficaram com a chave name-only — sem este
    /// backfill, uma Recarga da MESMA versão DNE (idempotente por design) não casaria o
    /// <c>ON CONFLICT (cep, nome_normalizado, cidade_id)</c> e inseriria duplicatas
    /// vigentes (o stale só marca versões anteriores). Não há mudança de schema.
    /// </summary>
    public partial class ReconciliaNomeNormalizadoLogradouroTextoCompleto : Migration
    {
        // translate() reproduz a remoção de acento do alfabeto pt-BR que GeoTexto faz em C#
        // (maiúsculas e minúsculas → ASCII), e lower() canonicaliza a caixa — batendo com o
        // que o ETL grava a partir de logradouro_sem_acento. É uma migration one-shot (não
        // viola a regra de não usar unaccent em runtime). O texto completo é mais distintivo
        // que o name-only, então o recálculo nunca introduz colisão na UNIQUE.
        private const string Acentos = "àáâãäçèéêëìíîïñòóôõöùúûüÀÁÂÃÄÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜ";
        private const string SemAcento = "aaaaaceeeeiiiinooooouuuuAAAAACEEEEIIIINOOOOOUUUU";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Dropa o índice único antes do recálculo: um UPDATE de uma passada poderia
            // tropeçar na UNIQUE por colisão TRANSITÓRIA quando o novo valor de uma linha
            // iguala o valor ainda-atual de outra no mesmo CEP/cidade (rotação de chave,
            // ex.: "Rua A" novo "rua a" vs "Avenida Rua A" antigo "rua a"). Sem o índice o
            // UPDATE não verifica unicidade linha-a-linha. Tudo na transação da migration.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_logradouro_natural;");

            // Recalcula nome_normalizado para o texto completo sem acento. No-op em base vazia.
            migrationBuilder.Sql(
                $"UPDATE logradouro SET nome_normalizado = lower(translate(coalesce(nome_completo, nome), '{Acentos}', '{SemAcento}'));");

            // Remove duplicatas reais que o recálculo possa ter formado (nomes-fonte
            // degenerados com mesmo texto completo no CEP/cidade). O sobrevivente é escolhido
            // por vigente DESC, versao_dataset DESC, id DESC: a linha viva vence a stale (o
            // índice cobre stale também; manter só a stait esconderia o endereço dos filtros
            // read-side por vigente=true), depois o release mais novo, depois o id mais novo.
            migrationBuilder.Sql(
                """
                DELETE FROM logradouro l
                USING (
                    SELECT id, row_number() OVER (
                        PARTITION BY cep, nome_normalizado, cidade_id
                        ORDER BY vigente DESC, versao_dataset DESC, id DESC) AS rn
                    FROM logradouro
                ) ranked
                WHERE l.id = ranked.id AND ranked.rn > 1;
                """);

            // Recria o índice único idêntico ao da migration AddLogradouro (schema inalterado).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_logradouro_natural ON logradouro (cep, nome_normalizado, cidade_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Reconciliação de dados unidirecional: a forma name-only era o bug de busca/chave
            // (#707). Reverter reintroduziria a colisão de CEP-geral, então o Down é
            // intencionalmente no-op — schema e coluna não mudaram.
        }
    }
}
