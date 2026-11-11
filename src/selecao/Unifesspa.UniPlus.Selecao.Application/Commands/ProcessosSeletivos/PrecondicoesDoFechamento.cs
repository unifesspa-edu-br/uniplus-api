namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// O que o fechamento da sessão confere antes de qualquer consulta, na ordem do protocolo de
/// mutação (ADR-0110 D9): a precondição presente, a sessão aberta e a precondição válida.
/// </summary>
internal static class PrecondicoesDoFechamento
{
    /// <summary>A recusa da primeira condição que falha, ou nula quando o fechamento pode seguir.</summary>
    internal static DomainError? Recusa(PrecondicaoIfMatch precondicao, ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(precondicao);
        ArgumentNullException.ThrowIfNull(processo);

        // Incondicional nesta rota ("3 antes de 10"): ela existe para a sessão.
        if (!precondicao.Presente)
        {
            return new DomainError(
                "Precondicao.Requerida",
                "O fechamento encerra a sessão editorial em curso — informe o If-Match com o ETag dela.");
        }

        // A precondição precede as regras de negócio, e a antecipação é a mesma dos Definir*: um
        // cliente com ETag defasado tem de saber disso antes de o servidor ir conferir documento,
        // catálogo de atos e vaga de linhagem. O FecharRetificacao do agregado reconfere.
        return processo.Rascunho is null
            ? new DomainError("RascunhoRetificacao.NaoAberta", "Não há retificação em curso neste processo.")
            : processo.MutacaoBloqueada(precondicao);
    }
}
