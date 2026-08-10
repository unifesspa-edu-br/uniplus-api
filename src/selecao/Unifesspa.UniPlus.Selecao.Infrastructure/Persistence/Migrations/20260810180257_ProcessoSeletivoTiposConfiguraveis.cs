using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcessoSeletivoTiposConfiguraveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_processo_codigo",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_processo_nome",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_processo_origem_id",
                schema: "selecao",
                table: "processos_seletivos",
                type: "uuid",
                nullable: true,
                comment: "Id de origem do tipo de processo em Configuração, sem FK cross-schema; congelado na criação.");

            // A antiga coluna era enum persistido por inteiro. Não aceitamos 0
            // (Nenhum) nem valores desconhecidos: a migração deve falhar antes de
            // produzir um processo sem identidade de tipo rastreável.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM selecao.processos_seletivos
                        WHERE tipo NOT BETWEEN 1 AND 8)
                    THEN
                        RAISE EXCEPTION 'processos_seletivos.tipo contém valor sem mapeamento nos tipos de processo configuráveis';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE selecao.processos_seletivos
                SET
                    tipo_processo_origem_id = CASE tipo
                        WHEN 1 THEN '019fe8f8-1400-7000-8000-000000000001'::uuid
                        WHEN 2 THEN '019fe8f8-1400-7000-8000-000000000002'::uuid
                        WHEN 3 THEN '019fe8f8-1400-7000-8000-000000000003'::uuid
                        WHEN 4 THEN '019fe8f8-1400-7000-8000-000000000004'::uuid
                        WHEN 5 THEN '019fe8f8-1400-7000-8000-000000000005'::uuid
                        WHEN 6 THEN '019fe8f8-1400-7000-8000-000000000006'::uuid
                        WHEN 7 THEN '019fe8f8-1400-7000-8000-000000000007'::uuid
                        WHEN 8 THEN '019fe8f8-1400-7000-8000-000000000008'::uuid
                    END,
                    tipo_processo_codigo = CASE tipo
                        WHEN 1 THEN 'SiSU' WHEN 2 THEN 'PSIQ' WHEN 3 THEN 'PSECampo' WHEN 4 THEN 'PSVR'
                        WHEN 5 THEN 'TransferenciaInterna' WHEN 6 THEN 'TransferenciaExterna'
                        WHEN 7 THEN 'PortadorDiploma' WHEN 8 THEN 'Reopcao'
                    END,
                    tipo_processo_nome = CASE tipo
                        WHEN 1 THEN 'SiSU' WHEN 2 THEN 'Processo Seletivo Indígena e Quilombola'
                        WHEN 3 THEN 'Processo Seletivo Especial do Campo' WHEN 4 THEN 'Processo Seletivo Via Reserva'
                        WHEN 5 THEN 'Transferência Interna' WHEN 6 THEN 'Transferência Externa'
                        WHEN 7 THEN 'Portador de Diploma' WHEN 8 THEN 'Reopção'
                    END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "tipo_processo_codigo", schema: "selecao", table: "processos_seletivos",
                type: "character varying(64)", maxLength: 64, nullable: false,
                oldClrType: typeof(string), oldType: "character varying(64)", oldMaxLength: 64, oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "tipo_processo_nome", schema: "selecao", table: "processos_seletivos",
                type: "character varying(200)", maxLength: 200, nullable: false,
                oldClrType: typeof(string), oldType: "character varying(200)", oldMaxLength: 200, oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "tipo_processo_origem_id", schema: "selecao", table: "processos_seletivos",
                type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true,
                oldComment: "Id de origem do tipo de processo em Configuração, sem FK cross-schema; congelado na criação.",
                comment: "Id de origem do tipo de processo em Configuração, sem FK cross-schema; congelado na criação.");

            migrationBuilder.DropColumn(name: "tipo", schema: "selecao", table: "processos_seletivos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tipo", schema: "selecao", table: "processos_seletivos", type: "integer", nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM selecao.processos_seletivos
                        WHERE tipo_processo_codigo NOT IN (
                            'SiSU', 'PSIQ', 'PSECampo', 'PSVR', 'TransferenciaInterna',
                            'TransferenciaExterna', 'PortadorDiploma', 'Reopcao'))
                    THEN
                        RAISE EXCEPTION 'não é possível reverter: existem tipos de processo configuráveis sem valor no enum legado';
                    END IF;
                END $$;

                UPDATE selecao.processos_seletivos
                SET tipo = CASE tipo_processo_codigo
                    WHEN 'SiSU' THEN 1 WHEN 'PSIQ' THEN 2 WHEN 'PSECampo' THEN 3 WHEN 'PSVR' THEN 4
                    WHEN 'TransferenciaInterna' THEN 5 WHEN 'TransferenciaExterna' THEN 6
                    WHEN 'PortadorDiploma' THEN 7 WHEN 'Reopcao' THEN 8
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "tipo", schema: "selecao", table: "processos_seletivos", type: "integer", nullable: false,
                oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.DropColumn(
                name: "tipo_processo_codigo",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "tipo_processo_nome",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "tipo_processo_origem_id",
                schema: "selecao",
                table: "processos_seletivos");

        }
    }
}
