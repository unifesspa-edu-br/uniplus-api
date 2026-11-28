namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

/// <summary>
/// O edital e o que a versão congela dos documentos: a classificação dos tipos de ato, os anexos e o período de
/// inscrição, e a conformidade legal. As conferências acumulam; o período só se resolve com os anexos aceitos, e a
/// conformidade legal só se avalia com o edital resolvido, porque lê o dia de referência dele. As causas dos anexos
/// pedidos ficam à parte (<paramref name="CausasDoPedido"/>): apontam o campo do pedido, e não o documento da configuração.
/// </summary>
internal sealed record DocumentosConferidos(
    DadosEdital? Dados,
    ResultadoConformidade? Conformidade,
    IReadOnlyDictionary<string, TipoDeAtoCongelado>? TiposDeAto,
    IReadOnlyList<FieldError> Causas,
    IReadOnlyList<FieldError> CausasDoPedido);

public sealed class ConferenciaDosDocumentosDaOperacao(
    ConferenciaDosAnexosDoEdital anexos,
    ConferenciaDeConformidadeLegal conformidadeLegal)
{
    internal async Task<DocumentosConferidos> ConferirAsync(
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        LeiturasDoCongelamento lidos,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> tipos = CongelamentoDosTiposDeAto.Resolver(processo, lidos.Cadastros.TiposDeAtoUnicos);
        Result<IReadOnlyList<AnexoDoEdital>> anexosConferidos = await anexos
            .ConferirAsync(processo.Id, edital.Anexos, origem.AnexosVigentes(herdaOsDaVersao: edital.Anexos is null), cancellationToken)
            .ConfigureAwait(false);
        Result<DadosEdital>? dados = ResolverOEdital(processo, edital, ato.Documento, anexosConferidos);
        bool anexosAceitos = anexosConferidos.IsSuccess;
        Result<ResultadoConformidade>? conformidade = anexosAceitos && dados is { IsSuccess: true }
            ? await AvaliarAsync(processo, dados.Value!, lidos, cancellationToken).ConfigureAwait(false)
            : null;

        return new DocumentosConferidos(
            anexosAceitos ? dados?.Value : null,
            conformidade?.Value,
            tipos.Value,
            [.. tipos.Errors, .. dados?.Errors ?? [], .. conformidade?.Errors ?? []],
            anexosConferidos.Errors);
    }

    // O período depende só do cronograma e do pedido: a recusa dele sai mesmo com anexo recusado, que então entra vazio na resolução e
    // o edital resolvido não segue adiante. Nulo quando o período não se resolve porque a fase que o ancora está sem janela, recusa que
    // a conferência da configuração já traz. O documento do edital já foi conferido: o identificador vem dele, e não do pedido, que
    // pode tê-lo ausente.
    private static Result<DadosEdital>? ResolverOEdital(
        ProcessoSeletivo processo, EditalDeclarado edital, DocumentoAPublicar documento, Result<IReadOnlyList<AnexoDoEdital>> anexosConferidos) =>
        ResolucaoDoPeriodoDeInscricao.Resolver(
            processo, edital.Numero, edital.PeriodoInscricaoInicio, edital.PeriodoInscricaoFim, documento.Id, anexosConferidos.IsSuccess ? anexosConferidos.Value! : []);

    private Task<Result<ResultadoConformidade>> AvaliarAsync(
        ProcessoSeletivo processo, DadosEdital dados, LeiturasDoCongelamento lidos, CancellationToken cancellationToken) =>
        conformidadeLegal.AvaliarAsync(processo, dados.DiaDeReferenciaLegal(lidos.Fuso), lidos.Catalogo.FatosDeModalidade, cancellationToken);
}
