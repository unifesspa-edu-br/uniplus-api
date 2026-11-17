namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using Kernel.Results;

using Services.Rascunhos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="ExcluirRascunhoDeProcessoSeletivoCommand"/>: precondição ausente, bloqueio da linha, processo já publicado
/// ou inexistente, precondição que não casa, e só então apaga a linha e, na mesma transação, os documentos a publicar dela. Os
/// objetos desses documentos saem do armazenamento pela mensagem que acompanha a resposta. Sem chave de idempotência, a exceção
/// de concorrência propaga.
/// </summary>
public static class ExcluirRascunhoDeProcessoSeletivoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Mensagens)> Handle(
        ExcluirRascunhoDeProcessoSeletivoCommand command,
        BloqueioDoRascunhoDeProcessoSeletivo bloqueio,
        IRascunhoDeProcessoSeletivoRepository rascunhos,
        IDocumentoAPublicarRepository documentos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(bloqueio);
        ArgumentNullException.ThrowIfNull(rascunhos);
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<RascunhoDeProcessoSeletivo> rascunho = await bloqueio.BloquearEConferirAsync(command.Id, command.Precondicao, cancellationToken).ConfigureAwait(false);
        return rascunho.IsFailure
            ? (Result.ValidationFailure(rascunho.Errors), [])
            : await ExcluirAsync(rascunho.Value!, rascunhos, documentos, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(Result Resposta, IEnumerable<object> Mensagens)> ExcluirAsync(
        RascunhoDeProcessoSeletivo rascunho,
        IRascunhoDeProcessoSeletivoRepository rascunhos,
        IDocumentoAPublicarRepository documentos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> chaves = await documentos.RemoverDoProcessoAsync(rascunho.Id, cancellationToken).ConfigureAwait(false);
        rascunhos.Remover(rascunho);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return (Result.Success(), chaves.Count == 0 ? [] : [new RemoverArquivosDoRascunhoExcluido(chaves)]);
    }
}
