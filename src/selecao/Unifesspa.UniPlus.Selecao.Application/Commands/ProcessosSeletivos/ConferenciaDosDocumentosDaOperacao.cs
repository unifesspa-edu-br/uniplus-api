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
/// conformidade legal só se avalia com o edital resolvido, porque lê o dia de referência dele.
/// </summary>
internal sealed record DocumentosConferidos(
    DadosEdital? Dados,
    ResultadoConformidade? Conformidade,
    IReadOnlyDictionary<string, TipoDeAtoCongelado>? TiposDeAto,
    IReadOnlyList<FieldError> Causas);

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
        Result<DadosEdital>? dados = await ConferirOEditalAsync(processo, edital, origem, cancellationToken).ConfigureAwait(false);
        Result<ResultadoConformidade>? conformidade = dados is { IsSuccess: true }
            ? await AvaliarAsync(processo, dados.Value!, lidos, cancellationToken).ConfigureAwait(false)
            : null;

        return new DocumentosConferidos(
            dados?.Value, conformidade?.Value, tipos.Value, [.. tipos.Errors, .. dados?.Errors ?? [], .. conformidade?.Errors ?? []]);
    }

    // Os anexos recusados impedem resolver o período: sem eles o edital não se monta. Nulo quando o período não se resolve
    // porque a fase que o ancora está sem janela, recusa que a conferência da configuração já traz.
    private async Task<Result<DadosEdital>?> ConferirOEditalAsync(
        ProcessoSeletivo processo, EditalDeclarado edital, OrigemDoCongelamento origem, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<AnexoDoEdital>> conferidos = await anexos
            .ConferirAsync(processo.Id, edital.Anexos, origem.AnexosVigentes(herdaOsDaVersao: edital.Anexos is null), cancellationToken)
            .ConfigureAwait(false);

        if (conferidos.IsFailure)
        {
            return Result<DadosEdital>.ValidationFailure(conferidos.Errors);
        }

        return ResolucaoDoPeriodoDeInscricao.Resolver(
            processo, edital.Numero, edital.PeriodoInscricaoInicio, edital.PeriodoInscricaoFim, edital.DocumentoEditalId, conferidos.Value!);
    }

    private Task<Result<ResultadoConformidade>> AvaliarAsync(
        ProcessoSeletivo processo, DadosEdital dados, LeiturasDoCongelamento lidos, CancellationToken cancellationToken) =>
        conformidadeLegal.AvaliarAsync(processo, dados.DiaDeReferenciaLegal(lidos.Fuso), lidos.Catalogo.FatosDeModalidade, cancellationToken);
}
