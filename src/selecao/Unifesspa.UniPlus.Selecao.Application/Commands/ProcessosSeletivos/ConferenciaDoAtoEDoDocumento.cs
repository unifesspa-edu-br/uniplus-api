namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// O ato declarado, o documento do edital, o tipo do ato conferido, os atos criadores da linhagem do processo e o
/// instante em que a operação publica, o único que ela usa.
/// </summary>
internal readonly record struct AtoEDocumentoConferidos(
    DadosDoAto Ato,
    DocumentoAPublicar Documento,
    TipoAtoPublicadoView TipoDoAto,
    IReadOnlyList<Guid> AtosDaLinhagem,
    InstanteDaPublicacao Publicacao);

/// <summary>
/// O que as três transições que congelam uma versão conferem antes de qualquer escrita: o documento
/// confirmado e o tipo do ato no catálogo de Publicações. A vaga que a linhagem reserva sobre o certame é conferida
/// pelo registro do ato, na mesma transação da publicação. Os atributos conferidos do tipo seguem ao registro por
/// valor, porque reler o catálogo faria a decisão valer outra coisa. O instante da publicação é lido aqui, depois da
/// trava do processo que o handler já tomou, e o tipo do ato vigora no dia civil dele no fuso institucional: a data
/// que o documento declara não escolhe o tipo.
/// </summary>
public sealed class ConferenciaDoAtoEDoDocumento(
    IDocumentoAPublicarRepository documentos,
    IProcessoSeletivoRepository processos,
    ITipoAtoPublicadoReader tiposDeAto,
    LeituraDoInstanteDaPublicacao instantes)
{
    internal async Task<Result<AtoEDocumentoConferidos>> ConferirAsync(
        Guid processoId, Guid documentoEditalId, DadosDoAto ato, string momento, CancellationToken cancellationToken)
    {
        Result<DocumentoAPublicar> documento = await ConferenciaDoDocumentoDoEdital
            .ConferirAsync(documentos, processoId, documentoEditalId, momento, cancellationToken)
            .ConfigureAwait(false);

        return documento.IsFailure
            ? Result<AtoEDocumentoConferidos>.Failure(documento.Error!)
            : await ConferirOTipoEAVagaAsync(processoId, ato, documento.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoEDocumentoConferidos>> ConferirOTipoEAVagaAsync(
        Guid processoId, DadosDoAto ato, DocumentoAPublicar documento, CancellationToken cancellationToken)
    {
        Result<InstanteDaPublicacao> instante = instantes.Ler();

        return instante.IsFailure
            ? Result<AtoEDocumentoConferidos>.Failure(instante.Error!)
            : await ConferirOTipoNoDiaAsync(processoId, ato, documento, instante.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoEDocumentoConferidos>> ConferirOTipoNoDiaAsync(
        Guid processoId, DadosDoAto ato, DocumentoAPublicar documento, InstanteDaPublicacao instante, CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> tipoDoAto = await ConferenciaDoTipoDeAto
            .CongelaConfiguracaoAsync(tiposDeAto, ato, instante.Dia, cancellationToken)
            .ConfigureAwait(false);
        if (tipoDoAto.IsFailure)
        {
            return Result<AtoEDocumentoConferidos>.Failure(tipoDoAto.Error!);
        }

        IReadOnlyList<Guid> atosDaLinhagem = await processos
            .ObterAtosCriadoresAsync(processoId, cancellationToken)
            .ConfigureAwait(false);

        return Result<AtoEDocumentoConferidos>.Success(new(ato, documento, tipoDoAto.Value!, atosDaLinhagem, instante));
    }
}
