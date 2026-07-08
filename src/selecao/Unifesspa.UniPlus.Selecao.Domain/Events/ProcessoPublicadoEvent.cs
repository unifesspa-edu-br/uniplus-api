namespace Unifesspa.UniPlus.Selecao.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

/// <summary>
/// Emitido por <see cref="Entities.ProcessoSeletivo.Publicar"/> (RN08, Story
/// #759). Carrega os identificadores forenses completos — <see cref="EditalId"/>,
/// <see cref="SnapshotPublicacaoId"/> e os hashes — para que qualquer
/// consumidor (drenado via cascading messages, ADR-0005) tenha o fato
/// completo, sem precisar reconsultar o agregado.
/// </summary>
public sealed record ProcessoPublicadoEvent(
    Guid ProcessoSeletivoId,
    Guid EditalId,
    Guid SnapshotPublicacaoId,
    string HashConfiguracao,
    string HashEdital,
    DateTimeOffset OccurredOn) : DomainEventBase(OccurredOn);
