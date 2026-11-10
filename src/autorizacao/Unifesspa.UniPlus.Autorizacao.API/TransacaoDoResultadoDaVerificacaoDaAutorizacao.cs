namespace Unifesspa.UniPlus.Autorizacao.API;

using JasperFx;
using JasperFx.CodeGeneration;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine.Configuration;
using Wolverine.Persistence;
using Wolverine.Persistence.Sagas;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Põe o handler do resultado da verificação dos PDFs da Autorização numa transação do banco do módulo, com o outbox na
/// mesma gravação. Os receptores chegam ao handler como coleção, e o detector de transação do Wolverine só enxerga o
/// <c>DbContext</c> por dependência direta; a entidade que o módulo grava nomeia o contexto sem a Application conhecê-lo.
/// </summary>
internal sealed class TransacaoDoResultadoDaVerificacaoDaAutorizacao : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(container);

        foreach (HandlerChain chain in chains.Where(EhDoHandlerDaAutorizacao))
        {
            if (!rules.TryFindPersistenceFrameProvider(container, typeof(DocumentoDaVersao), out IPersistenceFrameProvider? provider))
            {
                throw new InvalidOperationException("Não há persistência do Wolverine para abrir a transação do resultado da verificação da Autorização.");
            }

            provider!.ApplyTransactionSupport(chain, container, typeof(DocumentoDaVersao));
        }
    }

    private static bool EhDoHandlerDaAutorizacao(HandlerChain chain) =>
        chain.MessageType == typeof(ResultadoDaVerificacaoDeArquivo)
        && chain.Handlers.Any(h => h.HandlerType == typeof(RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler));
}
