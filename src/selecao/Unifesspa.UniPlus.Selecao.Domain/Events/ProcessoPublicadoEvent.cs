namespace Unifesspa.UniPlus.Selecao.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

/// <summary>
/// Emitido por <see cref="Entities.ProcessoSeletivo.Publicar"/> (RN08, Story
/// #759). Carrega os identificadores forenses completos — <see cref="EditalId"/>,
/// <see cref="SnapshotPublicacaoId"/> e os hashes — para que qualquer
/// consumidor (drenado via cascading messages, ADR-0005) tenha o fato
/// completo, sem precisar reconsultar o agregado.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SnapshotPublicacaoId"/> carrega, desde a ADR-0104, o id da
/// <see cref="Entities.VersaoConfiguracao"/> congelada na publicação. O NOME do
/// membro é deliberadamente preservado, e não é cosmético: ele é o contrato de
/// dois canais duráveis que sobrevivem a um deploy —
/// </para>
/// <list type="number">
///   <item>o envelope da fila durável (PostgreSQL, ADR-0004/0044), cujo corpo
///   JSON foi escrito pela versão ANTERIOR do binário e é desserializado pela
///   nova: um membro renomeado não casa, e o consumidor receberia
///   <see cref="System.Guid.Empty"/> — publicando um identificador zerado no
///   Kafka em vez de falhar;</item>
///   <item>o schema Avro <c>ProcessoPublicado</c> (subject
///   <c>processo_seletivo_events-value</c>), publicado no Schema Registry sob
///   compatibilidade BACKWARD.</item>
/// </list>
/// <para>
/// Renomear o membro exigiria drenar a fila e evoluir o schema — trabalho de
/// migração de contrato, não de renomeação de agregado. O vocabulário novo vive
/// no domínio; o nome antigo, nos canais que já o carregam.
/// </para>
/// </remarks>
public sealed record ProcessoPublicadoEvent(
    Guid ProcessoSeletivoId,
    Guid EditalId,
    Guid SnapshotPublicacaoId,
    string HashConfiguracao,
    string HashEdital,
    DateTimeOffset OccurredOn) : DomainEventBase(OccurredOn);
