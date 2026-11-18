namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using Abstractions;

/// <summary>
/// Remove cada objeto da mensagem. O armazenamento trata como concluída a remoção do objeto que já não existe, então a
/// reentrega depois de uma falha no meio da lista repete o que já saiu sem efeito; a exceção propaga para a retentativa
/// do Wolverine.
/// </summary>
public static class RemoverArquivosDoRascunhoExcluidoHandler
{
    public static async Task Handle(
        RemoverArquivosDoRascunhoExcluido mensagem, IArquivoArmazenadoStorage storage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(storage);

        foreach (string chave in mensagem.Chaves)
        {
            await storage.RemoverAsync(chave, cancellationToken).ConfigureAwait(false);
        }
    }
}
