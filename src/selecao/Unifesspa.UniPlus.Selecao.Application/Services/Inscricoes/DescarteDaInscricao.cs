namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>Confere o descarte e prepara a remoção do rascunho e a limpeza posterior dos arquivos.</summary>
public sealed class DescarteDaInscricao(
    PeriodoDaInscricaoResolver periodo,
    VersaoDaInscricaoResolver versao,
    IInscricaoRepository inscricoes,
    IDocumentoDaInscricaoRepository documentos,
    TimeProvider timeProvider)
{
    public async Task<Result> DescartarAsync(Inscricao inscricao, PrecondicaoIfMatch precondicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        DomainError? foraDoPeriodo = (await periodo.ConferirAsync(inscricao.ProcessoSeletivoId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))?.Recusa;
        if (foraDoPeriodo is not null)
        {
            return Result.Failure(foraDoPeriodo);
        }
        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return adotada;
        }
        return inscricao.ConferirDescarte(precondicao) is { } recusa
            ? Result.Failure(recusa)
            : await RemoverAsync(inscricao, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> RemoverAsync(Inscricao inscricao, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArquivoDaApresentacao> arquivos = await documentos
            .ListarArquivosDaInscricaoAsync(inscricao.Id, cancellationToken).ConfigureAwait(false);
        foreach (ArquivoDaApresentacao arquivo in arquivos)
        {
            // A URL de envio ainda vale até ExpiraEm e pode recriar o objeto no armazenamento.
            await AgendaDeLimpezaDoArquivoDoCandidato.AgendarAsync(documentos, arquivo, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }
        await inscricoes.RemoverAsync(inscricao, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
