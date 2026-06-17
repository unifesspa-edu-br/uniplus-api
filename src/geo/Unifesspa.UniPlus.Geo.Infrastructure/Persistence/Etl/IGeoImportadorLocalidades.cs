namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

/// <summary>
/// Importador das folhas da hierarquia do Geo (Distrito/Bairro + faixas, CEP de grande
/// usuário, complementos e os ~1,4M logradouros) a partir do dataset DNE (ADR-0092,
/// Story #673). Complementa o <see cref="IGeoImportador"/> (topo País/Estado/Cidade,
/// #672): o País/Estado/Cidade precisa estar carregado antes, pois as folhas resolvem
/// as FKs <c>cidade_id</c>/<c>distrito_id</c>/<c>bairro_id</c> (int4 da fonte) para Guid.
/// </summary>
/// <remarks>
/// <para>Ao contrário do importador de topo (transação única), esta carga é um pipeline
/// de fases que commitam separadamente: o COPY em lote do logradouro e o
/// <c>CREATE INDEX CONCURRENTLY</c> não podem viver numa transação única. A idempotência
/// vem do upsert por chave natural (distrito/bairro) e de COPY → staging →
/// <c>ON CONFLICT</c> (logradouro/complemento).</para>
/// </remarks>
internal interface IGeoImportadorLocalidades
{
    /// <summary>
    /// Importa as folhas a partir da <paramref name="fonte"/> no <paramref name="modo"/>
    /// indicado. Órfãos (FK <c>cidade_id</c> ausente) e dados sujos são tolerados (vão
    /// ao relatório, não abortam); falha de infraestrutura aborta a fase corrente.
    /// </summary>
    Task<RelatorioImportacao> ImportarAsync(IGeoFonteDados fonte, ModoCarga modo, CancellationToken cancellationToken);
}
