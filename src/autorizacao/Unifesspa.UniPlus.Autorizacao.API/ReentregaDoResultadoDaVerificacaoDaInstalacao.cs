namespace Unifesspa.UniPlus.Autorizacao.API;

using JasperFx;
using JasperFx.CodeGeneration;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Política de falha do consumo do resultado da verificação do documento da instalação. Sem regra, a primeira
/// exceção manda o resultado para a fila morta. A falha provável é transiente (banco, deadlock, entregas
/// concorrentes), e insistir resolve; o resultado sem veredicto é defeito do contrato e vai direto para a fila
/// morta. O mesmo tipo de mensagem tem uma cadeia por handler preso à fila, então a política só alcança a cadeia
/// do handler da Autorização.
/// </summary>
internal sealed class ReentregaDoResultadoDaVerificacaoDaInstalacao : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (HandlerChain chain in chains)
        {
            if (chain.MessageType != typeof(ResultadoDaVerificacaoDeArquivo)
                || !chain.Handlers.Any(h => h.HandlerType == typeof(RegistrarResultadoDaVerificacaoDoDocumentoDaInstalacaoHandler)))
            {
                continue;
            }

            chain.OnException<ResultadoDaVerificacaoInvalidoException>()
                .MoveToErrorQueue();

            chain.OnException<Exception>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
                .Then.MoveToErrorQueue();
        }
    }
}
