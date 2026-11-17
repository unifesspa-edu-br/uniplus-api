namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// O que o fechamento e o descarte da sessão conferem antes de qualquer consulta, na ordem do protocolo de mutação
/// (ADR-0110 D9): a precondição presente, a sessão aberta e a precondição válida.
/// </summary>
internal static class PrecondicoesDoEncerramentoDaSessao
{
    /// <summary>
    /// A recusa da primeira condição que falha, ou nula quando o encerramento pode seguir. O <paramref name="encerramento"/>
    /// é o sujeito das mensagens: "O fechamento" ou "O descarte".
    /// </summary>
    /// <remarks>
    /// A precondição é incondicional nestas rotas, porque elas existem para a sessão: a falta dela é falha de protocolo,
    /// respondida antes de conferir se a sessão existe. E ela precede as regras de negócio, como nos <c>Definir*</c>: um
    /// cliente com ETag defasado tem de saber disso antes de o servidor conferir documento, catálogo de atos e vaga de
    /// linhagem, ou repor a configuração. A operação do agregado reconfere.
    /// </remarks>
    internal static DomainError? Recusa(PrecondicaoIfMatch precondicao, ProcessoSeletivo processo, string encerramento)
    {
        ArgumentNullException.ThrowIfNull(precondicao);
        ArgumentNullException.ThrowIfNull(processo);

        if (!precondicao.Presente)
        {
            return new DomainError(
                "Precondicao.Requerida",
                $"{encerramento} encerra a sessão editorial em curso — informe o If-Match com o ETag dela.");
        }

        return processo.Rascunho is null
            ? new DomainError("RascunhoRetificacao.NaoAberta", "Não há retificação em curso neste processo.")
            : processo.MutacaoBloqueada(precondicao);
    }
}
