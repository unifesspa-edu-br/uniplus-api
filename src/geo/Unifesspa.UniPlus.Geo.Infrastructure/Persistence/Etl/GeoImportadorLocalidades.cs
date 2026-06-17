namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Bulk;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

/// <summary>
/// Orquestra a carga das folhas do Geo (ADR-0092, Story #673) em duas fases que commitam
/// separadamente (o COPY em lote e o <c>CREATE INDEX CONCURRENTLY</c> não cabem numa
/// transação única): (1) <see cref="GeoImportadorDistritoBairro"/> faz o upsert de
/// Distrito/Bairro (+faixas) e grande usuário numa transação e devolve os mapas de FK;
/// (2) <see cref="LogradouroCopyImporter"/> carrega complementos e logradouros em lote.
/// O topo (País/Estado/Cidade, #672) deve estar carregado antes — a resolução de FK
/// depende das cidades.
/// </summary>
internal sealed partial class GeoImportadorLocalidades : IGeoImportadorLocalidades
{
    private readonly GeoImportadorDistritoBairro _distritoBairro;
    private readonly LogradouroCopyImporter _logradouro;
    private readonly ILogger<GeoImportadorLocalidades> _logger;

    public GeoImportadorLocalidades(
        GeoImportadorDistritoBairro distritoBairro,
        LogradouroCopyImporter logradouro,
        ILogger<GeoImportadorLocalidades> logger)
    {
        ArgumentNullException.ThrowIfNull(distritoBairro);
        ArgumentNullException.ThrowIfNull(logradouro);
        ArgumentNullException.ThrowIfNull(logger);

        _distritoBairro = distritoBairro;
        _logradouro = logradouro;
        _logger = logger;
    }

    public async Task<RelatorioImportacao> ImportarAsync(IGeoFonteDados fonte, ModoCarga modo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fonte);

        RelatorioImportacao relatorio = new(fonte.Versao);
        LogCargaIniciada(_logger, fonte.Versao, modo);

        ResolucaoLocalidades resolucao =
            await _distritoBairro.ImportarAsync(fonte, relatorio, cancellationToken).ConfigureAwait(false);

        await _logradouro.ImportarAsync(fonte, modo, resolucao, relatorio, cancellationToken).ConfigureAwait(false);

        int inseridos = Total(relatorio, t => t.Inseridos);
        int atualizados = Total(relatorio, t => t.Atualizados);
        int orfaos = Total(relatorio, t => t.Orfaos);
        int degradados = Total(relatorio, t => t.ParsesDegradados);
        LogCargaConcluida(_logger, fonte.Versao, inseridos, atualizados, orfaos, degradados);

        return relatorio;
    }

    private static int Total(RelatorioImportacao relatorio, Func<ContadorTabela, int> seletor) =>
        relatorio.Tabelas.Values.Sum(seletor);

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: carga das folhas iniciada (versão {Versao}, modo {Modo}).")]
    private static partial void LogCargaIniciada(ILogger logger, string versao, ModoCarga modo);

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: carga das folhas concluída (versão {Versao}, inseridos={Inseridos}, atualizados={Atualizados}, órfãos={Orfaos}, degradados={Degradados}).")]
    private static partial void LogCargaConcluida(ILogger logger, string versao, int inseridos, int atualizados, int orfaos, int degradados);
}
