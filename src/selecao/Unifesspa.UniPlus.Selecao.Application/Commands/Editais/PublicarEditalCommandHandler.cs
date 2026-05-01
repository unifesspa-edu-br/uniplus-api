namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do <see cref="PublicarEditalCommand"/> — primeiro
/// fluxo de referência cascading messages do UniPlus, conforme ADR-0005.
///
/// <para>O retorno em tupla <c>(Result, IEnumerable&lt;object&gt;)</c> entrega
/// duas coisas no mesmo caminho:
/// <list type="bullet">
///   <item><description><c>Result</c>: resposta tipada que o
///   <see cref="Application.Abstractions.Messaging.ICommandBus"/> propaga até
///   o caller (controller/teste) — encapsula sucesso ou
///   <see cref="DomainError"/> de regra de negócio sem usar exceção para
///   fluxo esperado de erro.</description></item>
///   <item><description><c>IEnumerable&lt;object&gt;</c>: cascading messages
///   drenadas via <see cref="Domain.Entities.EntityBase.DequeueDomainEvents"/>.
///   O <c>CaptureCascadingMessages</c> do Wolverine percorre cada elemento e
///   instala o envelope no <c>wolverine_outgoing_envelopes</c> dentro da
///   <c>IEnvelopeTransaction</c> ativa — atomicidade write+evento conforme
///   ADR-0005.</description></item>
/// </list>
/// </para>
///
/// <para>Wolverine reconhece o padrão tupla `(TResponse, IEnumerable&lt;object&gt;)`
/// e separa a resposta do cascading; <c>InvokeAsync&lt;Result&gt;</c> retorna o
/// primeiro elemento, e o segundo é despachado pelas regras de roteamento
/// (PG queue + Kafka opcional, ver <c>SelecaoOutboxExtension</c>... no
/// <c>Program.cs</c>).</para>
/// </summary>
public sealed class PublicarEditalCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarEditalCommand command,
        IEditalRepository editalRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(editalRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Edital? edital = await editalRepository.ObterPorIdAsync(command.EditalId, cancellationToken).ConfigureAwait(false);
        if (edital is null)
        {
            return (
                Result.Failure(new DomainError("Edital.NaoEncontrado", $"Edital '{command.EditalId}' não encontrado.")),
                []);
        }

        if (edital.Status == StatusEdital.Publicado)
        {
            return (
                Result.Failure(new DomainError("Edital.JaPublicado", $"Edital '{command.EditalId}' já está publicado.")),
                []);
        }

        edital.Publicar();
        editalRepository.Atualizar(edital);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // Padrão canônico ADR-0005: drenagem por cascading messages.
        // Cast<object> garante o switch case `IEnumerable<object>` em
        // MessageContext.EnqueueCascadingAsync sem depender de covariância
        // implícita do IDomainEvent (interface) para object.
        return (Result.Success(), edital.DequeueDomainEvents().Cast<object>());
    }
}
