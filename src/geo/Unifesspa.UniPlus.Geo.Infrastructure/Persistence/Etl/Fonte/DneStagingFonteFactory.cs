namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

/// <summary>
/// <see cref="IGeoFonteDadosFactory"/> de produção: constrói uma <see cref="DneStagingFonte"/>
/// sobre o banco do Geo (schema de staging configurável). A connection string vem da
/// <c>ConnectionStrings:GeoDb</c> — <strong>nunca</strong> de
/// <c>DbContext.Database.GetConnectionString()</c>, que o Npgsql devolve sem a senha após
/// a primeira abertura (lição da Story #673).
/// </summary>
internal sealed class DneStagingFonteFactory : IGeoFonteDadosFactory
{
    private readonly string _connectionString;
    private readonly string _schema;

    public DneStagingFonteFactory(IConfiguration configuration, IOptions<EtlOpcoes> opcoes)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(opcoes);

        _connectionString = configuration.GetConnectionString("GeoDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:GeoDb não configurada — necessária para o ETL DNE do Geo.");
        _schema = opcoes.Value.StagingSchema;
    }

    public IGeoFonteDados Criar(string versao) => new DneStagingFonte(_connectionString, versao, _schema);
}
