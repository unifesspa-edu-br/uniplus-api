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
    InstanteDoComando Publicacao);

/// <summary>
/// O que as três transições que congelam uma versão conferem antes de qualquer escrita: o documento
/// confirmado e o tipo do ato no catálogo de Publicações. A vaga que a linhagem reserva sobre o certame é conferida
/// pelo registro do ato, na mesma transação da publicação. Os atributos conferidos do tipo seguem ao registro por
/// valor, porque reler o catálogo faria a decisão valer outra coisa. O instante da operação é lido aqui, uma só vez,
/// depois da trava do processo que o handler já tomou, e o tipo do ato vigora no dia civil dele no fuso institucional: a
/// data que o documento declara não escolhe o tipo. A origem diz qual instante vale: na retificação, o efetivo, que não
/// precede a vigência da versão sucedida.
/// </summary>
public sealed class ConferenciaDoAtoEDoDocumento(
    IDocumentoAPublicarRepository documentos,
    IProcessoSeletivoRepository processos,
    ConferenciaDoTipoDeAto tiposDeAto,
    LeituraDoInstanteDoComando instantes)
{
    internal async Task<Result<AtoEDocumentoConferidos>> ConferirAsync(
        Guid processoId, Guid documentoEditalId, DadosDoAto ato, OrigemDoCongelamento origem, CancellationToken cancellationToken)
    {
        Result<DocumentoAPublicar> documento = await ConferenciaDoDocumentoDoEdital
            .ConferirAsync(documentos, processoId, documentoEditalId, origem.DescricaoDaOperacao, cancellationToken)
            .ConfigureAwait(false);

        return documento.IsFailure
            ? Result<AtoEDocumentoConferidos>.ValidationFailure(documento.Errors)
            : await ConferirOTipoEAVagaAsync(processoId, ato, documento.Value!, origem, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoEDocumentoConferidos>> ConferirOTipoEAVagaAsync(
        Guid processoId, DadosDoAto ato, DocumentoAPublicar documento, OrigemDoCongelamento origem, CancellationToken cancellationToken)
    {
        Result<InstanteDoComando> instante = origem.LerOInstante(instantes);

        return instante.IsFailure
            ? Result<AtoEDocumentoConferidos>.Failure(instante.Error!)
            : await ConferirOTipoNoDiaAsync(processoId, ato, documento, instante.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoEDocumentoConferidos>> ConferirOTipoNoDiaAsync(
        Guid processoId, DadosDoAto ato, DocumentoAPublicar documento, InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> tipoDoAto = await tiposDeAto
            .CongelaConfiguracaoAsync(ato, instante.Dia, cancellationToken)
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
