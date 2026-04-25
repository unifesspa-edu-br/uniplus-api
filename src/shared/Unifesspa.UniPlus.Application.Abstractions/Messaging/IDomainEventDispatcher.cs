namespace Unifesspa.UniPlus.Application.Abstractions.Messaging;

using Unifesspa.UniPlus.Kernel.Domain.Events;

/// <summary>
/// Publica domain events para os handlers registrados, intra-módulo (in-process) ou
/// cross-módulo (via Kafka transport configurado no Wolverine). Aplicação publica eventos
/// como <see cref="IDomainEvent"/> — tópicos, partições e headers são detalhe de routing
/// gerenciado pelo Wolverine. Ver ADR-022 (uniplus-docs/docs/adrs/).
/// </summary>
/// <remarks>
/// <para>
/// <b>Quando usar este dispatcher direto:</b> notificações fire-and-forget que NÃO refletem
/// mudança de estado persistida (telemetria, side-effects derivados da request). Para eventos
/// que representam fato consumado em uma transação (a maioria dos casos), use
/// <c>entity.AddDomainEvent(...)</c> + <c>SaveChangesAsync</c> — Wolverine intercepta o
/// SaveChanges e despacha via outbox transacional, garantindo atomicidade write+evento.
/// </para>
/// <para>
/// <b>Comportamento de cancelamento:</b> se <paramref name="ct"/> já estiver cancelado quando
/// <see cref="Publish"/> é chamado, o método lança <see cref="OperationCanceledException"/> e
/// <b>não publica o evento</b>. Isso é coerente com o uso esperado (fire-and-forget de
/// side-effect derivado da request — se o caller cancelou, o side-effect não deve acontecer),
/// mas significa que eventos podem ser suprimidos em cenários de cancelamento. Se o evento
/// representa fato consumado e perda é inaceitável, use o caminho via outbox descrito acima.
/// </para>
/// </remarks>
public interface IDomainEventDispatcher
{
    Task Publish(IDomainEvent domainEvent, CancellationToken ct = default);
}
