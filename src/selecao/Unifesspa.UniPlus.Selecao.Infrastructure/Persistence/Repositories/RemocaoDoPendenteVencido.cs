namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// O passo transacional que os repositórios de arquivo enviado compartilham para remover o pendente
/// vencido: o DELETE condicionado segura a linha, os objetos saem do armazenamento e só então a
/// transação é confirmada. A transação é própria — só a rotina de limpeza chama, fora de qualquer
/// transação ambiente.
/// </summary>
internal static class RemocaoDoPendenteVencido
{
    /// <summary>
    /// Devolve <see langword="true"/> quando <paramref name="removerRegistro"/> afetou uma linha e os
    /// objetos saíram; sem linha afetada nada é removido do armazenamento. Falha em qualquer passo
    /// desfaz a transação e propaga.
    /// </summary>
    public static async Task<bool> ExecutarAsync(
        DatabaseFacade banco,
        Func<CancellationToken, Task<int>> removerRegistro,
        Func<CancellationToken, Task> removerObjetos,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction transacao = await banco.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (transacao.ConfigureAwait(false))
        {
            if (await removerRegistro(cancellationToken).ConfigureAwait(false) != 1)
            {
                return false;
            }

            await removerObjetos(cancellationToken).ConfigureAwait(false);
            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
