namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using NetTopologySuite.Geometries;

/// <summary>
/// Helpers de mapeamento comuns às configurações das entidades de localidade
/// (reference data) do Geo: o tipo da coluna geográfica (ADR-0091) e a
/// proveniência de release (<c>versao_dataset</c>/<c>vigente</c>, ADR-0092),
/// presentes em todas as 14 entidades. Mantém o mapeamento consistente sem
/// repetir as mesmas linhas em cada configuração.
/// </summary>
internal static class GeoConfiguracaoExtensions
{
    /// <summary>
    /// Tipo PostGIS canônico das coordenadas (WGS84). O espaço antes do parêntese
    /// espelha o tipo materializado pelo plugin NetTopologySuite do Npgsql — manter
    /// idêntico evita drift de migration. A coluna é <c>nullable</c> nas entidades
    /// de localidade (nem toda linha tem coordenada na fonte); o índice GIST
    /// continua válido para consultas espaciais sobre as linhas preenchidas.
    /// </summary>
    public const string TipoColunaGeografia = "geography (Point, 4326)";

    // versao_dataset é AAAAMM (6 chars); o teto folgado tolera drift de formato em
    // releases futuras sem migration (filosofia anti-frágil do ETL, ADR-0092).
    private const int VersaoDatasetMaxLength = 20;

    /// <summary>Mapeia a coordenada para <c>geography(Point,4326)</c> (sem índice — o GIST é nomeado por entidade).</summary>
    public static void ConfigurarCoordenada<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Point?>> coordenada)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Property(coordenada).HasColumnType(TipoColunaGeografia);
    }

    /// <summary>
    /// Mapeia a proveniência de release comum a todo reference data (ADR-0092):
    /// <c>versao_dataset</c> (AAAAMM, <c>NOT NULL</c>) e <c>vigente</c>
    /// (<c>NOT NULL</c>, default DDL <c>true</c> — cobre inserts crus do ETL que
    /// omitam a coluna). O índice em <c>versao_dataset</c> que sustenta a política
    /// de stale fica nomeado por entidade.
    /// </summary>
    /// <remarks>
    /// <c>vigente</c> usa <c>HasDefaultValue(true)</c> <strong>+ <c>ValueGeneratedNever()</c></strong>:
    /// sem o <c>ValueGeneratedNever</c>, o EF trataria <c>false</c> (default CLR de
    /// <c>bool</c>) como "não informado" e omitiria a coluna no INSERT, deixando o
    /// banco gravar o default <c>true</c> — tornando impossível persistir
    /// <c>vigente = false</c> (a própria marcação de stale). Com <c>ValueGeneratedNever</c>,
    /// o EF sempre envia o valor real da propriedade; o default DDL permanece para
    /// inserts crus (COPY do ETL) que omitam a coluna.
    /// </remarks>
    public static void ConfigurarProveniencia<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, string>> versaoDataset,
        Expression<Func<TEntity, bool>> vigente)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Property(versaoDataset).HasMaxLength(VersaoDatasetMaxLength).IsRequired();
        builder.Property(vigente).IsRequired().HasDefaultValue(true).ValueGeneratedNever();
    }
}
