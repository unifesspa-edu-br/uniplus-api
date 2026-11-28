namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere os documentos que o ato referencia: cada um é do processo, está confirmado, foi
/// enviado para um ato e não foi usado em outro; o principal é PDF e os anexos têm nomes distintos. Cada documento
/// recusado dá a sua causa, no campo dele, e os demais seguem conferidos.
/// </summary>
/// <remarks>
/// Antes de qualquer escrita, porque o ato é gravado e registrado numa transação só e a recusa tardia
/// desfaria o que já foi rastreado. O documento usado em outro ato é recusado para que o
/// arquivo publicado tenha um único ato por trás; a finalidade já separa o documento do ato do
/// documento e dos anexos do edital. Um documento recusado no envio não segue para as etapas seguintes, e as causas saem
/// agrupadas por etapa — envio, formato, nomes, uso — e, dentro dela, pela posição no pedido. O uso em outro ato depende só do identificador do
/// documento: confere todo documento que o envio aceitou, mesmo com o formato ou o nome recusados.
/// </remarks>
public sealed class ConferenciaDosDocumentosDoAto(
    IDocumentoAPublicarRepository documentoRepository,
    IAtoDoProcessoRepository atoDoProcessoRepository)
{
    internal async Task<ResultadoDosDocumentos> ConferirAsync(
        Guid processoSeletivoId, PedidoDosDocumentosDoAto pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!pedido.PodeLer)
        {
            return ResultadoDosDocumentos.NaoLido;
        }

        Dictionary<Guid, DocumentoAPublicar> encontrados = await LerAsync(processoSeletivoId, pedido.ALer, cancellationToken).ConfigureAwait(false);
        EtapaDosDocumentos envios = Conferir(pedido.ALer, documento => ConferirEnvio(documento, encontrados.GetValueOrDefault(documento.Id)));
        EtapaDosDocumentos formato = Conferir(envios.Aprovados, ConferirFormatoDoPrincipal);
        EtapaDosDocumentos nomes = ConferirNomes(formato.Aprovados);
        EtapaDosDocumentos usos = await ConferirUsosAsync(processoSeletivoId, envios.Aprovados, cancellationToken).ConfigureAwait(false);

        return Reunir(pedido, [envios, formato, nomes, usos], nomes.Aprovados);
    }

    private async Task<Dictionary<Guid, DocumentoAPublicar>> LerAsync(
        Guid processoId, IReadOnlyList<DocumentoDoPedido> documentos, CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentoAPublicar> encontrados = await documentoRepository
            .ListarDoProcessoAsync(processoId, [.. documentos.Select(static d => d.Id)], cancellationToken).ConfigureAwait(false);

        return encontrados.ToDictionary(static d => d.Id);
    }

    private static EtapaDosDocumentos Conferir<TEntrada>(
        IEnumerable<TEntrada> entradas, Func<TEntrada, Result<DocumentoLido>> conferir)
    {
        Result<DocumentoLido>[] resultados = [.. entradas.Select(conferir)];

        return new(
            [.. resultados.SelectMany(static r => r.Errors)],
            [.. resultados.Where(static r => r.IsSuccess).Select(static r => r.Value!)]);
    }

    private static Result<DocumentoLido> ConferirEnvio(DocumentoDoPedido pedido, DocumentoAPublicar? documento) => documento switch
    {
        null => Falha(pedido.Campo, AtoDoProcessoErrorCodes.DocumentoNaoEncontrado, pedido.EhPrincipal
            ? "O documento principal do ato deve ser um dos documentos deste processo."
            : "O documento do anexo deve ser um dos documentos deste processo."),
        { Situacao: not StatusArquivoEnviado.Confirmado } => Falha(pedido.Campo, AtoDoProcessoErrorCodes.DocumentoNaoConfirmado,
            "Somente um documento confirmado pode ser referenciado no ato."),
        { Finalidade: not FinalidadeDoDocumento.DocumentoDeAto } => Falha(pedido.Campo, AtoDoProcessoErrorCodes.DocumentoNaoEnviadoComoDocumentoDeAto,
            "Somente um documento enviado para um ato pode ser o principal ou o anexo de um ato."),
        _ => Result<DocumentoLido>.Success(new(pedido, documento)),
    };

    private static Result<DocumentoLido> ConferirFormatoDoPrincipal(DocumentoLido lido) =>
        lido.Pedido.EhPrincipal && lido.Documento.Formato != FormatoDeDocumento.Pdf
            ? Falha(lido.Pedido.Campo, AtoDoProcessoErrorCodes.DocumentoPrincipalNaoEhPdf, "O documento principal do ato deve ser um PDF.")
            : Result<DocumentoLido>.Success(lido);

    private static EtapaDosDocumentos ConferirNomes(IReadOnlyList<DocumentoLido> lidos)
    {
        HashSet<string> nomesUsados = new(NomeDoAnexoPublicado.Comparador);

        return Conferir(lidos, lido => ConferirNome(lido, nomesUsados));
    }

    private static Result<DocumentoLido> ConferirNome(DocumentoLido lido, HashSet<string> nomesUsados)
    {
        if (lido.Pedido.EhPrincipal)
        {
            return Result<DocumentoLido>.Success(lido);
        }

        string? nome = NomeDeArquivoEnviado.Normalizar(lido.Pedido.NomeDoAnexo, lido.Documento.Extensao);

        return nome is null
            ? Falha(lido.Pedido.CampoDoNome, AtoDoProcessoErrorCodes.AnexoNomeInvalido,
                $"O nome do anexo é obrigatório, tem até {NomeDeArquivoEnviado.TamanhoMaximo} caracteres com a extensão e não tem separador de caminho, caractere de controle nem {NomeDeArquivoEnviado.CaracteresProibidos}.")
            : ConferirUnicidadeDoNome(lido, nome, nomesUsados);
    }

    private static Result<DocumentoLido> ConferirUnicidadeDoNome(DocumentoLido lido, string nome, HashSet<string> nomesUsados) =>
        nomesUsados.Add(NomeDoAnexoPublicado.Normalizar(nome))
            ? Result<DocumentoLido>.Success(lido with { Nome = nome })
            : Falha(lido.Pedido.CampoDoNome, AtoDoProcessoErrorCodes.AnexoNomeRepetido,
                "Há mais de um anexo com o mesmo nome no ato. O nome do download leva a extensão do arquivo, então 'quadro' e 'quadro.pdf' são o mesmo nome.");

    private async Task<EtapaDosDocumentos> ConferirUsosAsync(
        Guid processoId, IReadOnlyList<DocumentoLido> lidos, CancellationToken cancellationToken)
    {
        if (lidos.Count == 0)
        {
            return new([], []);
        }

        IReadOnlySet<Guid> jaUsados = await atoDoProcessoRepository
            .ListarDocumentosJaUsadosAsync(processoId, [.. lidos.Select(static l => l.Documento.Id)], cancellationToken).ConfigureAwait(false);

        return Conferir(lidos, lido => jaUsados.Contains(lido.Documento.Id)
            ? Falha(lido.Pedido.Campo, AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto,
                "O documento já foi usado em outro ato do processo. Envie o arquivo de novo para publicá-lo neste ato.")
            : Result<DocumentoLido>.Success(lido));
    }

    private static ResultadoDosDocumentos Reunir(
        PedidoDosDocumentosDoAto pedido, IReadOnlyList<EtapaDosDocumentos> etapas, IReadOnlyList<DocumentoLido> aprovadosEmTodas)
    {
        FieldError[] recusas = [.. etapas.SelectMany(static e => e.Recusas)];
        bool conferidoPorInteiro = recusas.Length == 0 && pedido.Recusas.Count == 0;

        return new(recusas, conferidoPorInteiro ? Montar(aprovadosEmTodas) : null);
    }

    private static DocumentosDoAtoConferidos Montar(IReadOnlyList<DocumentoLido> lidos) =>
        new(
            lidos.Single(static l => l.Pedido.EhPrincipal).Documento,
            [.. lidos.Where(static l => !l.Pedido.EhPrincipal).Select(static l => new AnexoConferido(l.Documento, l.Nome!))]);

    private static Result<DocumentoLido> Falha(string campo, string codigo, string mensagem) =>
        Result<DocumentoLido>.ValidationFailure([new FieldError(campo, new DomainError(codigo, mensagem))]);
}

/// <summary>Um documento que passou pelas etapas da conferência até aqui, com o nome do download quando é anexo.</summary>
internal sealed record DocumentoLido(DocumentoDoPedido Pedido, DocumentoAPublicar Documento, string? Nome = null);

/// <summary>O que uma etapa da conferência dos documentos recusou e o que deixou seguir para a etapa seguinte.</summary>
internal sealed record EtapaDosDocumentos(IReadOnlyList<FieldError> Recusas, IReadOnlyList<DocumentoLido> Aprovados);

/// <summary>
/// O que a conferência dos documentos concluiu: as causas, e os documentos quando nenhuma causa os impede.
/// </summary>
/// <param name="Recusas">As causas, no campo de cada documento.</param>
/// <param name="Conferidos">Os documentos conferidos; nulo quando há causa ou os documentos não foram lidos.</param>
internal sealed record ResultadoDosDocumentos(IReadOnlyList<FieldError> Recusas, DocumentosDoAtoConferidos? Conferidos)
{
    public static ResultadoDosDocumentos NaoLido { get; } = new([], null);
}

/// <summary>Os documentos do ato depois da conferência: o principal e os anexos com o nome do download.</summary>
internal sealed record DocumentosDoAtoConferidos(DocumentoAPublicar Principal, IReadOnlyList<AnexoConferido> Anexos);

/// <summary>Um anexo conferido: o documento e o nome, já com a extensão do formato.</summary>
internal sealed record AnexoConferido(DocumentoAPublicar Documento, string Nome);
