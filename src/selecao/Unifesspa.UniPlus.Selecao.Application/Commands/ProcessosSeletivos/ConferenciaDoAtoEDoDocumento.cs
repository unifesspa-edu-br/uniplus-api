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
/// confirmado, o tipo do ato no catálogo de Publicações e a vaga que a linhagem reserva sobre o
/// certame. O ato é registrado depois, por mensagem durável (ADR-0108), e a recusa do catálogo tem de
/// vir agora, com 422: do contrário o certame sai publicado e o registro vira dead letter. Os atributos
/// conferidos viajam na mensagem, porque reler o catálogo no consumo faria a decisão valer outra coisa. O instante da
/// publicação é lido aqui, depois da trava do processo que o handler já tomou, e o tipo do ato vigora no dia civil dele
/// no fuso institucional: a data que o documento declara não escolhe o tipo.
/// </summary>
public sealed class ConferenciaDoAtoEDoDocumento(
    IDocumentoAPublicarRepository documentos,
    IProcessoSeletivoRepository processos,
    ITipoAtoPublicadoReader tiposDeAto,
    IVagaDeLinhagemReader vagasDeLinhagem,
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
        Result<InstanteDaPublicacao> publicacao = instantes.Ler();

        return publicacao.IsFailure
            ? Result<AtoEDocumentoConferidos>.Failure(publicacao.Error!)
            : await ConferirOTipoNoDiaAsync(processoId, ato, documento, publicacao.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoEDocumentoConferidos>> ConferirOTipoNoDiaAsync(
        Guid processoId, DadosDoAto ato, DocumentoAPublicar documento, InstanteDaPublicacao publicacao, CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> tipoDoAto = await ConferenciaDoTipoDeAto
            .CongelaConfiguracaoAsync(tiposDeAto, ato, publicacao.Dia, cancellationToken)
            .ConfigureAwait(false);
        if (tipoDoAto.IsFailure)
        {
            return Result<AtoEDocumentoConferidos>.Failure(tipoDoAto.Error!);
        }

        Result<IReadOnlyList<Guid>> atosDaLinhagem = await ConferirAVagaAsync(processoId, tipoDoAto.Value!, cancellationToken).ConfigureAwait(false);

        return atosDaLinhagem.IsFailure
            ? Result<AtoEDocumentoConferidos>.Failure(atosDaLinhagem.Error!)
            : Result<AtoEDocumentoConferidos>.Success(new(ato, documento, tipoDoAto.Value!, atosDaLinhagem.Value!, publicacao));
    }

    // A vaga da linhagem é monotônica: ocupada por outra linhagem, o registro do ato seria
    // recusado no consumo da fila e o certame ficaria publicado sem ato.
    private async Task<Result<IReadOnlyList<Guid>>> ConferirAVagaAsync(
        Guid processoId, TipoAtoPublicadoView tipoDoAto, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> atosDaLinhagem = await processos
            .ObterAtosCriadoresAsync(processoId, cancellationToken)
            .ConfigureAwait(false);
        Result vaga = await ConferenciaDoTipoDeAto
            .VagaDoObjetoAsync(vagasDeLinhagem, tipoDoAto, processoId, atosDaLinhagem, cancellationToken)
            .ConfigureAwait(false);

        return vaga.IsFailure
            ? Result<IReadOnlyList<Guid>>.Failure(vaga.Error!)
            : Result<IReadOnlyList<Guid>>.Success(atosDaLinhagem);
    }
}
