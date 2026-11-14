namespace Unifesspa.UniPlus.Selecao.API;

using JasperFx;
using JasperFx.CodeGeneration;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Política de falha da mensagem que copia os documentos de um ato ao acervo público e registra os endereços na
/// divulgação do certame — e de mais nenhuma.
/// </summary>
/// <remarks>
/// <para>
/// Sem uma regra que case, a PRIMEIRA exceção manda o envelope direto para a fila morta. O certame já está divulgado,
/// e o efeito de uma cópia que desiste é um documento que nunca ganha endereço: a falha não volta a ninguém, porque o
/// consumo é assíncrono.
/// </para>
/// <para>
/// As falhas prováveis desse consumo são transientes: indisponibilidade do banco ou do acervo público, deadlock, e a
/// janela de um deploy em que o pod que consome ainda não conhece a versão de schema que o pod que publicou congelou.
/// Insistir resolve todas; desistir na primeira não resolve nenhuma.
/// </para>
/// <para>
/// Vive aqui, e não como <c>Configure(HandlerChain)</c> no próprio handler, porque o handler está em Application, onde
/// a regra de arquitetura proíbe depender do Wolverine além dos atributos. Declarar a regra em
/// <c>opts.Policies.OnException</c> a tornaria global, e como todo command HTTP passa por <c>ICommandBus.Send</c>, que
/// aplica as políticas inline, uma falha de validação em qualquer módulo passaria a esperar a sequência de cooldown
/// antes de responder ao cliente.
/// </para>
/// </remarks>
internal sealed class ReentregaDaCopiaAoAcervo : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (HandlerChain chain in chains.Where(static c => c.MessageType == typeof(CopiarDocumentosDoAtoAoAcervo)))
        {
            // A primeira regra que casa é a que vale: as específicas vêm antes da geral.
            ReagendarEmMinutos<EnvelopeAindaNaoLegivelException>(chain);
            ReagendarEmMinutos<AcervoPublicoIndisponivelException>(chain);
            InsistirEmSegundos(chain);
        }
    }

    // Envelope que este processo ainda não sabe ler não melhora em segundos: a causa esperada é a janela de um deploy em
    // fases, em que um processo já atualizado congela numa versão que este só conhecerá ao ser substituído. A
    // indisponibilidade do armazenamento (reinício, bucket público ainda não provisionado no deploy) tem a mesma escala.
    // Reentrega imediata esgotaria as tentativas antes de a janela fechar; o reagendamento devolve a mensagem à fila e
    // libera o consumidor, e quem processar depois pode ser outro processo, já novo.
    private static void ReagendarEmMinutos<TExcecao>(HandlerChain chain)
        where TExcecao : Exception =>
        chain.OnException<TExcecao>()
            .ScheduleRetry(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15))
            .Then.MoveToErrorQueue();

    // Falha transiente (indisponibilidade momentânea, deadlock) é o caso em que insistir resolve em segundos.
    private static void InsistirEmSegundos(HandlerChain chain) =>
        chain.OnException<Exception>()
            .RetryWithCooldown(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
            .Then.MoveToErrorQueue();
}
