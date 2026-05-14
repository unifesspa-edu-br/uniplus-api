namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Emite o exclusion constraint GIST das junction tables de
/// <c>AreasDeInteresse</c> (ADR-0060). O EF Core não expressa
/// <c>EXCLUDE USING GIST</c> no fluent API — o resto da junction table
/// (colunas, PK, FK, índice parcial) vem do modelo EF via
/// <see cref="Configurations.AreaVisibilityConfiguration{TParent}"/>; só
/// esta constraint é SQL bruto, emitido na migration logo após o
/// <c>CreateTable</c> gerado pelo EF.
/// </summary>
/// <remarks>
/// Requer a extensão <c>btree_gist</c> provisionada no banco — em dev via
/// <c>docker/init-db.sql</c>; em standalone/HML/PROD via a primeira migration
/// do DbContext (<c>CREATE EXTENSION IF NOT EXISTS btree_gist</c>).
/// </remarks>
public static class JunctionTableMigrationHelper
{
    /// <summary>
    /// SQL do exclusion constraint que impede janelas de validade sobrepostas
    /// para o mesmo <c>(parent, área)</c> numa junction table.
    /// </summary>
    /// <param name="junctionTable">Nome da junction table (ex.: <c>modalidade_areas_de_interesse</c>).</param>
    /// <param name="parentForeignKeyColumn">Nome da coluna FK para o pai (ex.: <c>modalidade_id</c>).</param>
    public static string ExclusionConstraintSql(string junctionTable, string parentForeignKeyColumn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(junctionTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentForeignKeyColumn);

        return $"""
            ALTER TABLE {junctionTable}
              ADD CONSTRAINT excl_{junctionTable}_overlap
              EXCLUDE USING GIST (
                {parentForeignKeyColumn} WITH =,
                area_codigo WITH =,
                tstzrange(valid_from, valid_to, '[)') WITH &&
              );
            """;
    }

    /// <summary>
    /// Adiciona o exclusion constraint GIST à junction table via
    /// <see cref="MigrationBuilder.Sql(string, bool)"/>. Chamado na migration
    /// logo após o <c>CreateTable</c> gerado pelo EF para a junction.
    /// </summary>
    public static void AddAreaDeInteresseExclusionConstraint(
        this MigrationBuilder migrationBuilder,
        string junctionTable,
        string parentForeignKeyColumn)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(ExclusionConstraintSql(junctionTable, parentForeignKeyColumn));
    }
}
