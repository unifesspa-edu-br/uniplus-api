namespace Unifesspa.UniPlus.Selecao.API;

using JasperFx;
using JasperFx.CodeGeneration;

using Unifesspa.UniPlus.Publicacoes.Contracts;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Política de falha da mensagem que materializa a divulgação pública do certame — e de mais
/// nenhuma.
/// </summary>
/// <remarks>
/// <para>
/// Sem uma regra que case, a PRIMEIRA exceção manda o envelope direto para a fila morta. Como a
/// existência da linha de divulgação <b>é</b> a publicidade do certame, o efeito é um certame que
/// nunca aparece embora o ato esteja registrado — e a falha não volta a ninguém, porque o
/// consumo é assíncrono.
/// </para>
/// <para>
/// As falhas prováveis desse consumo são todas transientes: indisponibilidade momentânea do banco,
/// deadlock, conflito de gravação entre duas entregas do mesmo ato, e a janela de um deploy em que
/// o pod que consome ainda não conhece a versão de schema que o pod que publicou congelou.
/// Insistir resolve todas; desistir na primeira não resolve nenhuma. Mesma sequência de espera do
/// handler que registra o ato, do outro lado da fila.
/// </para>
/// <para>
/// Vive aqui, e não como <c>Configure(HandlerChain)</c> no próprio handler, porque o handler está
/// em Application, onde a regra de arquitetura proíbe depender do Wolverine além dos atributos.
/// Como política de chain, alcança a mesma chain sem levar o framework para dentro da camada.
/// Declarar a regra em <c>opts.Policies.OnException</c> a tornaria global, e como todo command
/// HTTP passa por <c>ICommandBus.Send</c>, que aplica as políticas inline, uma falha de validação
/// em qualquer módulo passaria a esperar a sequência de cooldown antes de responder ao cliente.
/// </para>
/// </remarks>
internal sealed class ReentregaDaDivulgacaoDoCertame : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (HandlerChain chain in chains)
        {
            if (chain.MessageType != typeof(AtoNormativoRegistrado))
            {
                continue;
            }

            chain.OnException<Exception>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
                .Then.MoveToErrorQueue();
        }
    }
}
