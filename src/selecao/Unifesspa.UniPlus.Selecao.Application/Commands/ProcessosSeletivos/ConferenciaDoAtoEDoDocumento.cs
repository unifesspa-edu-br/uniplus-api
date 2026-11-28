namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

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
/// depois da trava do processo que o handler já tomou e antes de qualquer consulta, e o tipo do ato vigora no dia
/// civil dele no fuso institucional: a data que o documento declara não escolhe o tipo. A origem diz qual instante
/// vale: na retificação, o efetivo, que não precede a vigência da versão sucedida. Sem o fuso institucional, a falha
/// é da instituição e responde sozinha. Com o instante, a forma do pedido, o documento do edital e o tipo do ato não
/// dependem uns dos outros: as recusas de cada um se somam na mesma resposta, cada uma no campo do pedido que a
/// corrige, e a falta do campo de um só impede a consulta dele.
/// </summary>
public sealed class ConferenciaDoAtoEDoDocumento(
    IDocumentoAPublicarRepository documentos,
    IProcessoSeletivoRepository processos,
    ConferenciaDoTipoDeAto tiposDeAto,
    ConferenciaDosAnexosDoEdital anexos,
    LeituraDoInstanteDoComando instantes)
{
    internal async Task<Result<AtoEDocumentoConferidos>> ConferirAsync(
        Guid processoId,
        EditalDeclarado edital,
        DadosDoAto? ato,
        OrigemDoCongelamento origem,
        CancellationToken cancellationToken)
    {
        Result<InstanteDoComando> instante = origem.LerOInstante(instantes);

        return instante.IsFailure
            ? Result<AtoEDocumentoConferidos>.Failure(instante.Error!)
            : await ConferirNoInstanteAsync(processoId, edital, ato, origem, instante.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtoEDocumentoConferidos>> ConferirNoInstanteAsync(
        Guid processoId,
        EditalDeclarado edital,
        DadosDoAto? ato,
        OrigemDoCongelamento origem,
        InstanteDoComando instante,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FieldError> ausentes = AusenciasNaDeclaracaoDoEdital.Listar(edital.DocumentoEditalId, ato, edital.Anexos);
        Result<DocumentoAPublicar>? documento = await ConferirODocumentoAsync(processoId, edital, origem, cancellationToken).ConfigureAwait(false);
        Result<TipoAtoPublicadoView>? tipoDoAto = await ConferirOTipoAsync(ato, instante, cancellationToken).ConfigureAwait(false);
        FieldError[] causas = [.. ausentes, .. documento?.Errors ?? [], .. tipoDoAto?.Errors ?? []];

        return causas.Length > 0
            ? Result<AtoEDocumentoConferidos>.ValidationFailure([.. causas, .. await ConferirOsAnexosAsync(processoId, edital, cancellationToken).ConfigureAwait(false)])
            : await ReunirALinhagemAsync(
                processoId, AusenciasNaDeclaracaoDoEdital.Completar(ato)!, documento!.Value!, tipoDoAto!.Value!, instante, cancellationToken)
                .ConfigureAwait(false);
    }

    // Os anexos não dependem do documento do edital nem do tipo do ato: com qualquer outra recusa, a deles sai junto. Sem recusa, a
    // conferência dos anexos acontece depois, na conferência da configuração, que monta o edital com eles.
    private async Task<IReadOnlyList<FieldError>> ConferirOsAnexosAsync(
        Guid processoId, EditalDeclarado edital, CancellationToken cancellationToken)
    {
        if (edital.Anexos is not { } pedidos)
        {
            return [];
        }

        Result<IReadOnlyList<AnexoDoEdital>> conferidos = await anexos
            .ConferirAsync(processoId, pedidos, [], cancellationToken)
            .ConfigureAwait(false);

        return conferidos.IsSuccess ? [] : conferidos.Errors;
    }

    // Sem o documento no pedido, não há o que consultar: a ausência já saiu na forma.
    private async Task<Result<DocumentoAPublicar>?> ConferirODocumentoAsync(
        Guid processoId, EditalDeclarado edital, OrigemDoCongelamento origem, CancellationToken cancellationToken) =>
        edital.DocumentoEditalId is { } id
            ? await ConferenciaDoDocumentoDoEdital
                .ConferirAsync(documentos, processoId, id, origem.DescricaoDaOperacao, cancellationToken)
                .ConfigureAwait(false)
            : null;

    private async Task<Result<TipoAtoPublicadoView>?> ConferirOTipoAsync(
        DadosDoAto? ato, InstanteDoComando instante, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(ato?.TipoAtoCodigo)
            ? null
            : TipoNoCampoDoPedido(
                await tiposDeAto.CongelaConfiguracaoAsync(ato.TipoAtoCodigo, instante.Dia, cancellationToken).ConfigureAwait(false));

    // A conferência do tipo é partilhada com o ato avulso e não sabe de que campo do pedido ele veio: quem a chama o aponta.
    private static Result<TipoAtoPublicadoView> TipoNoCampoDoPedido(Result<TipoAtoPublicadoView> tipo) =>
        tipo.IsSuccess
            ? tipo
            : Result<TipoAtoPublicadoView>.ValidationFailure(
                [.. tipo.Errors.Select(static causa => causa with { Field = AusenciasNaDeclaracaoDoEdital.CampoDoTipoDoAto })]);

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
