using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "discentes");

            migrationBuilder.CreateTable(
                name: "sync_run",
                schema: "discentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) da execução."),
                    status = table.Column<int>(type: "integer", nullable: false, comment: "Estado da execução (Running/Completed/Partial/Failed)."),
                    total_items = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade total de itens previstos para esta execução."),
                    processed_items = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens já processados."),
                    success_count = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens processados com sucesso."),
                    error_count = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens que falharam durante o processamento."),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Instante de início da execução."),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Instante de conclusão da execução — nulo enquanto o status é Running."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_run", x => x.id);
                },
                comment: "Controle de execução das rotinas de sincronização de dados discentes com o SIGAA — uma linha por execução.");

            migrationBuilder.CreateTable(
                name: "vinculo_discente",
                schema: "discentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) do registro — não confundir com id_discente_sigaa, a chave natural do SIGAA."),
                    id_discente_sigaa = table.Column<long>(type: "bigint", nullable: false, comment: "Identificador do discente no SIGAA (chave natural do módulo) — usado para localizar e fazer upsert durante a sincronização."),
                    matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Matrícula do discente na instituição."),
                    cpf_ciphertext = table.Column<byte[]>(type: "bytea", nullable: false, comment: "CPF cifrado em repouso (AES-GCM, ADR-0119) — envelope autenticado (nonce + tag + dado); nunca texto claro."),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Nome do discente."),
                    nivel = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, comment: "Nível de ensino do vínculo (ex.: G para graduação) — vocabulário do SIGAA."),
                    curso_id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador do curso no SIGAA."),
                    curso_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Nome do curso."),
                    curso_codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Código e-MEC do curso, quando disponível."),
                    curso_unidade_id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador da unidade acadêmica responsável pelo curso, no SIGAA."),
                    curso_unidade_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Nome da unidade acadêmica responsável pelo curso."),
                    situacao_id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador da situação acadêmica do discente no SIGAA."),
                    situacao_descricao = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Descrição da situação acadêmica (ex.: Matriculado, Concluído)."),
                    situacao_vinculo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Qualificador de vínculo associado à situação, no vocabulário do SIGAA."),
                    ano_ingresso = table.Column<int>(type: "integer", nullable: false, comment: "Ano de ingresso do discente no curso."),
                    periodo_ingresso = table.Column<int>(type: "integer", nullable: false, comment: "Período letivo de ingresso do discente no curso.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculo_discente", x => x.id);
                },
                comment: "Réplica local dos vínculos de discentes sincronizados do SIGAA (ADR-0119) — snapshot desnormalizado, sem referência viva a outras tabelas.");

            migrationBuilder.CreateIndex(
                name: "ix_sync_run_started_at",
                schema: "discentes",
                table: "sync_run",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_discente_id_discente_sigaa",
                schema: "discentes",
                table: "vinculo_discente",
                column: "id_discente_sigaa",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_run",
                schema: "discentes");

            migrationBuilder.DropTable(
                name: "vinculo_discente",
                schema: "discentes");
        }
    }
}
