namespace Unifesspa.UniPlus.Spikes.EventSourcing.Application.EventosIntegracao;

/// <summary>
/// Evento de <b>integração</b> (contrato externo, ex.: Kafka) derivado da publicação.
/// É PII-free por design — distinto do evento canônico de domínio. Publicado como
/// cascading message: instalado no outbox na mesma transação do append (gate G1).
/// </summary>
public sealed record EditalPublicadoIntegrado(
    Guid EditalId,
    string NumeroEdital,
    DateTimeOffset OcorridoEm);
