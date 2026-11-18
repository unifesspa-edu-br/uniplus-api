namespace Unifesspa.UniPlus.Selecao.API;

using JasperFx;
using JasperFx.CodeGeneration;

using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Política de falha da mensagem que remove do armazenamento os objetos de um rascunho excluído — e de mais nenhuma.
/// </summary>
/// <remarks>
/// Sem uma regra que case, a primeira exceção manda o envelope para a fila morta e os objetos ficam no armazenamento sem
/// registro que os aponte. A falha provável é a indisponibilidade do armazenamento: insiste-se em segundos para a
/// falha momentânea e, esgotadas as tentativas, reagenda-se em minutos para a indisponibilidade que dura. A regra vive aqui
/// porque o handler está em Application, onde a arquitetura proíbe depender do Wolverine além dos atributos.
/// </remarks>
internal sealed class ReentregaDaRemocaoDosArquivosDoRascunho : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (HandlerChain chain in chains.Where(static c => c.MessageType == typeof(RemoverArquivosDoRascunhoExcluido)))
        {
            chain.OnException<Exception>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
                .Then.ScheduleRetry(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15))
                .Then.MoveToErrorQueue();
        }
    }
}
