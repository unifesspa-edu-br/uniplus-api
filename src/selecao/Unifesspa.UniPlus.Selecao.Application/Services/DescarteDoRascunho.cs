namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Descarta o rascunho do candidato. O <c>If-Match</c> que ele tem é o do rascunho já levado à versão divulgada, que é o que a leitura
/// devolve; conferir contra o gravado recusaria para sempre o ETag que ele recebeu. Os registros dos arquivos caem em cascata com a
/// inscrição, e os objetos do armazenamento saem pela rotina de limpeza, depois do commit.
/// </summary>
public sealed class DescarteDoRascunho(
    VersaoDaInscricao versao,
    IInscricaoRepository inscricoes,
    IDocumentoDaInscricaoRepository documentos,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Result> DescartarAsync(Inscricao inscricao, PrecondicaoIfMatch precondicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result.Failure(adotada.Error!);
        }

        return inscricao.ConferirDescarte(precondicao) is { } recusa
            ? Result.Failure(recusa)
            : await RemoverAsync(inscricao, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> RemoverAsync(Inscricao inscricao, CancellationToken cancellationToken)
    {
        await AgendarLimpezaDosArquivosAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await inscricoes.RemoverAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task AgendarLimpezaDosArquivosAsync(Inscricao inscricao, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArquivoDaApresentacao> arquivos = await documentos.ListarArquivosDaInscricaoAsync(inscricao.Id, cancellationToken).ConfigureAwait(false);
        foreach (ArquivoDaApresentacao arquivo in arquivos)
        {
            // A URL de envio ainda vale até ExpiraEm: um PUT nela recria o objeto no bucket de envio.
            await AgendaDeLimpezaDoArquivoDoCandidato.AgendarAsync(documentos, arquivo, relogio.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }
    }
}
