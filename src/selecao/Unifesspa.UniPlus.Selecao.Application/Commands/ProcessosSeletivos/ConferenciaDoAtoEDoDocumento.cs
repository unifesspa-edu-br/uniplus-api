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
    DadosDoAtoCompletos Ato,
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
/// precede a vigência da versão sucedida. O campo obrigatório ausente (o documento do edital, o bloco do ato, o ano, a data e o documento de cada anexo) acumula com a recusa do
/// tipo do ato e corta o que depende dele; o documento recusado impede o resto.
/// </summary>
public sealed class ConferenciaDoAtoEDoDocumento(
    IDocumentoAPublicarRepository documentos,
    IProcessoSeletivoRepository processos,
    ConferenciaDoTipoDeAto tiposDeAto,
    LeituraDoInstanteDoComando instantes)
{
    internal async Task<Result<AtoEDocumentoConferidos>> ConferirAsync(
        Guid processoId,
        Guid? documentoEditalId,
        DadosDoAto? ato,
        IReadOnlyList<AnexoSolicitadoDoEdital>? anexos,
        OrigemDoCongelamento origem,
        CancellationToken cancellationToken)
    {
        FieldError[] ausentes = [.. AusenciasNaDeclaracaoDoEdital.Listar(documentoEditalId, ato, anexos)];
        Result<DocumentoAPublicar>? documento = documentoEditalId is { } id
            ? await ConferenciaDoDocumentoDoEdital
                .ConferirAsync(documentos, processoId, id, origem.DescricaoDaOperacao, cancellationToken)
                .ConfigureAwait(false)
            : null;

        return documento is { IsFailure: true }
            ? Result<AtoEDocumentoConferidos>.ValidationFailure([.. ausentes, .. documento.Errors])
            : await ConferirOTipoEAVagaAsync(processoId, ausentes, ato, documento?.Value, origem, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoEDocumentoConferidos>> ConferirOTipoEAVagaAsync(
        Guid processoId,
        FieldError[] ausentes,
        DadosDoAto? ato,
        DocumentoAPublicar? documento,
        OrigemDoCongelamento origem,
        CancellationToken cancellationToken)
    {
        Result<InstanteDoComando> instante = origem.LerOInstante(instantes);

        return instante.IsFailure
            ? Result<AtoEDocumentoConferidos>.Failure(instante.Error!)
            : await ConferirOTipoNoDiaAsync(processoId, ausentes, ato, documento, instante.Value, cancellationToken).ConfigureAwait(false);
    }

    // A ausência de um campo acumula com a recusa do tipo, que não depende dele; sem causa, o documento e o tipo existem.
    private async Task<Result<AtoEDocumentoConferidos>> ConferirOTipoNoDiaAsync(
        Guid processoId,
        FieldError[] ausentes,
        DadosDoAto? ato,
        DocumentoAPublicar? documento,
        InstanteDoComando instante,
        CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView>? tipoDoAto = string.IsNullOrWhiteSpace(ato?.TipoAtoCodigo)
            ? null
            : await tiposDeAto.CongelaConfiguracaoAsync(ato.TipoAtoCodigo, instante.Dia, cancellationToken).ConfigureAwait(false);
        FieldError[] causas = [.. ausentes, .. tipoDoAto?.Errors ?? []];

        return causas.Length > 0
            ? Result<AtoEDocumentoConferidos>.ValidationFailure(causas)
            : await ReunirALinhagemAsync(
                processoId, AusenciasNaDeclaracaoDoEdital.Completar(ato)!, documento!, tipoDoAto!.Value!, instante, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<Result<AtoEDocumentoConferidos>> ReunirALinhagemAsync(
        Guid processoId,
        DadosDoAtoCompletos ato,
        DocumentoAPublicar documento,
        TipoAtoPublicadoView tipoDoAto,
        InstanteDoComando instante,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> atosDaLinhagem = await processos
            .ObterAtosCriadoresAsync(processoId, cancellationToken)
            .ConfigureAwait(false);

        return Result<AtoEDocumentoConferidos>.Success(new(ato, documento, tipoDoAto, atosDaLinhagem, instante));
    }
}
