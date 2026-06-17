namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

/// <summary>
/// Importador idempotente de reference data do Geo a partir do dataset DNE
/// (ADR-0092). A carga é um serviço transacional de <c>Geo.Infrastructure</c>
/// (não um command Wolverine — não há regra de negócio nem evento de domínio,
/// é carga de reference data autoritativo). A proveniência (<c>versao_dataset</c>)
/// vem da própria <see cref="IGeoFonteDados.Versao"/>.
/// </summary>
internal interface IGeoImportador
{
    /// <summary>
    /// Importa o topo da hierarquia (País → Estado → Cidade, com indicadores e faixas)
    /// numa única transação: upsert por chave natural (idempotente — reaplicar a mesma
    /// versão deixa o banco idêntico), órfãos e degradações tolerados (vão ao relatório,
    /// não abortam a carga). Falha de infraestrutura faz rollback total.
    /// </summary>
    Task<RelatorioImportacao> ImportarAsync(IGeoFonteDados fonte, CancellationToken cancellationToken);
}
