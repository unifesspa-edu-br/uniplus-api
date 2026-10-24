namespace Unifesspa.UniPlus.Selecao.API;

using JasperFx;
using JasperFx.CodeGeneration;

using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Política de falha do consumo do resultado da verificação. Sem regra, a primeira exceção manda o resultado para
/// a fila morta. A falha provável é transiente (banco, deadlock, entregas concorrentes), e insistir resolve; o
/// resultado sem veredicto é defeito do contrato e vai direto para a fila morta. Vive aqui porque a Application
/// não depende do Wolverine além dos atributos.
/// </summary>
internal sealed class ReentregaDoResultadoDaVerificacao : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (HandlerChain chain in chains)
        {
            if (chain.MessageType != typeof(ResultadoDaVerificacaoDeArquivo))
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
