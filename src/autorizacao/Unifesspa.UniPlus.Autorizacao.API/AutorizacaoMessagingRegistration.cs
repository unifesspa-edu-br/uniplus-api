namespace Unifesspa.UniPlus.Autorizacao.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

/// <summary>
/// Transporte do resultado da verificação do PDF de designação (ADR-0146). O pedido sai pela mesma quorum queue de
/// pedidos dos documentos do candidato, publicada pelo wiring da Seleção, com o destino da Autorização; o resultado
/// volta pela fila própria desta, e o handler da Autorização fica preso a ela.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class AutorizacaoMessagingRegistration
{
    /// <summary>
    /// Escuta a fila de resultados da Autorização. Precisa rodar depois de a Seleção ligar o transporte RabbitMQ, que
    /// é compartilhado. O handler é preso à fila: sem isso o resultado dos documentos do candidato executaria também o
    /// da instalação, e o contrário, porque os dois tratam o mesmo tipo de mensagem.
    /// </summary>
    public static void ConfigurarRouting(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.ListenToRabbitQueue(FilasDaVerificacao.ResultadosAutorizacao, fila => fila.QueueType = QueueType.quorum)
            .AddStickyHandler(typeof(RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler));

        opts.Policies.Add<ReentregaDoResultadoDaVerificacaoDaAutorizacao>();
    }
}
