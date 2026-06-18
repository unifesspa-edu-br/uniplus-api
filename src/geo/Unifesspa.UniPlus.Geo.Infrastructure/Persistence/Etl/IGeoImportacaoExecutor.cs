namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

/// <summary>
/// Executa a carga pesada de uma execução já registrada (Story #674). Porta interna
/// usada pelo <c>BackgroundService</c> — separada de <c>IGeoImportacaoService</c> (a
/// porta da API, que só dispara e acompanha), para que a borda não exponha a execução
/// longa. Implementada pelo mesmo orquestrador.
/// </summary>
internal interface IGeoImportacaoExecutor
{
    /// <summary>
    /// Executa a carga da execução <paramref name="execucaoId"/> (deriva o modo do estado
    /// da base, roda os importadores, marca linhas obsoletas, sela a versão vigente do cache
    /// e grava status + relatório). Não lança em falha de carga: marca a execução como
    /// <c>Falhou</c> e registra a métrica/log.
    /// </summary>
    Task ExecutarAsync(Guid execucaoId, CancellationToken cancellationToken);
}
