namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

/// <summary>
/// Modo da carga de logradouros do ETL DNE (ADR-0092). Define a estratégia de bulk:
/// <see cref="Inicial"/> assume base nova (trunca <c>logradouro</c> e dropa seus índices
/// pesados para COPY rápido, recriando-os ao fim); <see cref="Recarga"/> assume base
/// populada (mantém os índices e reconcilia por <c>ON CONFLICT</c>). A escolha do modo é
/// do gatilho operacional (Story de atualização periódica, #674); ambos são idempotentes.
/// </summary>
internal enum ModoCarga
{
    /// <summary>Carga inicial: TRUNCATE de <c>logradouro</c> + drop/recria dos seus índices pesados em torno do COPY (demais folhas reconciliam por upsert/ON CONFLICT).</summary>
    Inicial,

    /// <summary>Recarga periódica: COPY para staging + <c>ON CONFLICT DO UPDATE</c>, índices preservados.</summary>
    Recarga,
}
