namespace Unifesspa.UniPlus.Selecao.API;

using JasperFx;
using JasperFx.CodeGeneration;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

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

            // Envelope que este processo ainda não sabe ler não melhora em segundos: a causa
            // esperada é a janela de um deploy em fases, em que um processo já atualizado congela
            // numa versão que este só conhecerá ao ser substituído. Reentrega imediata esgotaria as
            // tentativas antes de a janela fechar, e o certame ficaria invisível com o ato
            // registrado. O reagendamento devolve a mensagem à fila e libera o consumidor, em
            // escala de minutos — e quem processar depois pode ser outro processo, já novo.
            //
            // Declarada ANTES da regra geral: a primeira política que casa é a que vale.
            chain.OnException<EnvelopeAindaNaoLegivelException>()
                .ScheduleRetry(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15))
                .Then.MoveToErrorQueue();

            // Endereço público já ocupado por outro certame divulgado: dado inconsistente, que
            // nenhuma reentrega corrige. Vai direto para a fila morta, com a exceção que nomeia o
            // processo e o identificador.
            chain.OnException<IdentificadorLegivelJaDivulgadoException>()
                .MoveToErrorQueue();

            // Falha transiente — indisponibilidade momentânea, deadlock, conflito entre duas
            // entregas do mesmo ato — é o caso em que insistir resolve em segundos.
            chain.OnException<Exception>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
                .Then.MoveToErrorQueue();
        }
    }
}
