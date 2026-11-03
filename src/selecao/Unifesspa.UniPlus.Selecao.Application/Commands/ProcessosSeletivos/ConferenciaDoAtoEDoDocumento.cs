namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>O documento do edital, o tipo do ato conferido e os atos criadores da linhagem do processo.</summary>
internal readonly record struct AtoEDocumentoConferidos(
    DocumentoAPublicar Documento, TipoAtoPublicadoView TipoDoAto, IReadOnlyList<Guid> AtosDaLinhagem);

/// <summary>
/// O que as três transições que congelam uma versão conferem antes de qualquer escrita: o documento
/// confirmado, o tipo do ato no catálogo de Publicações e a vaga que a linhagem reserva sobre o
/// certame. O ato é registrado depois, por mensagem durável (ADR-0108), e a recusa do catálogo tem de
/// vir agora, com 422: do contrário o certame sai publicado e o registro vira dead letter. Os atributos
/// conferidos viajam na mensagem, porque reler o catálogo no consumo faria a decisão valer outra coisa.
/// </summary>
internal static class ConferenciaDoAtoEDoDocumento
{
    internal static async Task<Result<AtoEDocumentoConferidos>> ConferirAsync(
        IDocumentoAPublicarRepository documentos,
        IProcessoSeletivoRepository processos,
        ITipoAtoPublicadoReader tiposDeAto,
        IVagaDeLinhagemReader vagasDeLinhagem,
        Guid processoId,
        Guid documentoEditalId,
        DadosDoAto ato,
        string momento,
        CancellationToken cancellationToken)
    {
        Result<DocumentoAPublicar> documento = await ConferenciaDoDocumentoDoEdital
            .ConferirAsync(documentos, processoId, documentoEditalId, momento, cancellationToken)
            .ConfigureAwait(false);
        if (documento.IsFailure)
        {
            return Result<AtoEDocumentoConferidos>.Failure(documento.Error!);
        }

        Result<TipoAtoPublicadoView> tipoDoAto = await ConferenciaDoTipoDeAto
            .CongelaConfiguracaoAsync(tiposDeAto, ato, cancellationToken)
            .ConfigureAwait(false);
        if (tipoDoAto.IsFailure)
        {
            return Result<AtoEDocumentoConferidos>.Failure(tipoDoAto.Error!);
        }

        // A vaga da linhagem é monotônica: ocupada por outra linhagem, o registro do ato seria
        // recusado no consumo da fila e o certame ficaria publicado sem ato.
        IReadOnlyList<Guid> atosDaLinhagem = await processos
            .ObterAtosCriadoresAsync(processoId, cancellationToken)
            .ConfigureAwait(false);

        Result vaga = await ConferenciaDoTipoDeAto
            .VagaDoObjetoAsync(vagasDeLinhagem, tipoDoAto.Value!, processoId, atosDaLinhagem, cancellationToken)
            .ConfigureAwait(false);
        if (vaga.IsFailure)
        {
            return Result<AtoEDocumentoConferidos>.Failure(vaga.Error!);
        }

        return Result<AtoEDocumentoConferidos>.Success(new(documento.Value!, tipoDoAto.Value!, atosDaLinhagem));
    }
}
