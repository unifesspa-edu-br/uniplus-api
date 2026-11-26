namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Resolve as <see cref="ObrigatoriedadeLegal"/> vigentes para o tipo do processo na
/// data de corte (Story #852 §3.1: <c>DadosEdital.PeriodoInscricaoInicio</c>), avalia
/// contra o agregado (<see cref="AvaliadorConformidadeLegal"/>) e recusa a transição
/// ANTES da canonicalização se qualquer regra reprovar — o mesmo momento em que o gate
/// estrutural (<c>PendenciaDeConformidade</c>) já recusa (ADR-0109 D5).
/// </summary>
/// <remarks>
/// <para>
/// Serviço compartilhado pela conferência do congelamento das duas operações (<c>Publicar</c> e
/// <c>Retificar</c>), chamado uma vez em cada, com o mesmo veredicto.
/// </para>
/// <para>
/// <b>Fonte única (CA-16):</b> <see cref="IObrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync"/>
/// + <see cref="AvaliadorConformidadeLegal.Avaliar"/> é exatamente a mesma dupla de
/// chamadas que <c>ObterConformidadeLegalProcessoSeletivoQueryHandler</c> usa, pelo
/// <see cref="LevantarAsync"/> — a regra que a consulta pública mostra reprovada é a
/// mesma que bloqueia a transição.
/// </para>
/// </remarks>
public sealed class ConferenciaDeConformidadeLegal(
    IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
    IModalidadeReader modalidadeReader,
    ITipoDocumentoReader tipoDocumentoReader,
    ITipoEtapaReader tipoEtapaReader,
    ITipoDeficienciaReader tipoDeficienciaReader,
    IRegraCatalogoReader regraCatalogoReader)
{
    /// <summary>
    /// Avalia o processo na data de referência e recusa se alguma regra vigente não puder ser avaliada ou reprovar.
    /// </summary>
    /// <remarks>
    /// O mesmo levantamento das referências alimenta a avaliação: a identidade que o gate usa para casar exigência
    /// com regra é a que a conferência acabou de ler, não uma segunda leitura que poderia divergir dela.
    /// </remarks>
    internal async Task<Result<ResultadoConformidade>> AvaliarAsync(
        ProcessoSeletivo processo,
        DateOnly dataReferencia,
        FatosDeModalidade fatosDeModalidade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);

        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes = await RegrasVigentesAsync(processo, dataReferencia, cancellationToken)
            .ConfigureAwait(false);
        Result<ConferenciaDeReferenciasDasRegras.RelatorioDeReferencias> referencias = await ConferirAsReferenciasAsync(regrasVigentes, cancellationToken)
            .ConfigureAwait(false);

        return referencias.IsFailure
            ? Result<ResultadoConformidade>.Failure(referencias.Error!)
            : SomenteAprovadas(AvaliadorConformidadeLegal.Avaliar(
                processo, processo.TipoProcesso.Codigo, regrasVigentes, referencias.Value!.Identidades, fatosDeModalidade));
    }

    /// <summary>
    /// Avalia o processo na data de referência sem recusar: a regra que não pode ser avaliada fica registrada no
    /// levantamento, e quem consulta a mostra reprovada com o motivo.
    /// </summary>
    /// <remarks>
    /// É a mesma sequência de <see cref="AvaliarAsync"/> — regras vigentes, referências, avaliação —, para que a consulta
    /// pública nunca divirja da transição (CA-16). Só muda o que se faz com a regra inavaliável: a transição a recusa, a
    /// consulta a relata. A regra que aparece reprovada é a mesma que bloqueia a publicação, inclusive quando o motivo é
    /// identidade e não código.
    /// </remarks>
    internal async Task<ConformidadeLegalLevantada> LevantarAsync(
        ProcessoSeletivo processo,
        DateOnly dataReferencia,
        FatosDeModalidade fatosDeModalidade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);

        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes = await RegrasVigentesAsync(processo, dataReferencia, cancellationToken)
            .ConfigureAwait(false);
        ConferenciaDeReferenciasDasRegras.RelatorioDeReferencias referencias = await ReferenciasLevantadasAsync(regrasVigentes, cancellationToken)
            .ConfigureAwait(false);
        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, processo.TipoProcesso.Codigo, regrasVigentes, referencias.Identidades, fatosDeModalidade);

        return new ConformidadeLegalLevantada(resultado, referencias.RegrasInavaliaveis);
    }

    private Task<ConferenciaDeReferenciasDasRegras.RelatorioDeReferencias> ReferenciasLevantadasAsync(
        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes, CancellationToken cancellationToken) =>
        ConferenciaDeReferenciasDasRegras.LevantarAsync(
            regrasVigentes, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, cancellationToken);

    private Task<IReadOnlyList<ObrigatoriedadeLegal>> RegrasVigentesAsync(
        ProcessoSeletivo processo, DateOnly dataReferencia, CancellationToken cancellationToken) =>
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
            processo.TipoProcesso.Codigo, dataReferencia, cancellationToken);

    // Antes de avaliar: uma regra que referencia cadastro inexistente é aprovada por vacuidade pelo avaliador, que é
    // domínio puro e não consulta cadastro. Publicar sob ela seria publicar sob obrigação que ninguém pode cumprir.
    private Task<Result<ConferenciaDeReferenciasDasRegras.RelatorioDeReferencias>> ConferirAsReferenciasAsync(
        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes, CancellationToken cancellationToken) =>
        ConferenciaDeReferenciasDasRegras.ConferirAsync(
            regrasVigentes, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, cancellationToken);

    private static Result<ResultadoConformidade> SomenteAprovadas(ResultadoConformidade resultado)
    {
        string[] reprovadas = [.. resultado.Regras.Where(static r => !r.Aprovada).Select(static r => r.RegraCodigo)];

        return reprovadas.Length > 0
            ? Result<ResultadoConformidade>.Failure(new DomainError(
                "ProcessoSeletivo.ConformidadeLegalInsuficiente",
                $"Processo não conforme às obrigatoriedades legais vigentes — reprovado(s): {string.Join(", ", reprovadas)}."))
            : Result<ResultadoConformidade>.Success(resultado);
    }
}

/// <summary>
/// O que a avaliação das regras vigentes levantou: o veredicto de cada regra e, para as que não podem ser avaliadas,
/// o motivo pelo qual não podem, indexado pelo identificador da regra.
/// </summary>
/// <param name="Resultado">O veredicto do avaliador, regra a regra, e os avisos que ele emitiu.</param>
/// <param name="RegrasInavaliaveis">Motivo pelo qual cada regra não pode ser avaliada; ausente quando a regra é avaliável.</param>
internal sealed record ConformidadeLegalLevantada(
    ResultadoConformidade Resultado,
    IReadOnlyDictionary<Guid, string> RegrasInavaliaveis)
{
    /// <summary>
    /// Motivo com que a regra aparece reprovada na leitura pública, ou <see langword="null"/> quando a regra pode ser
    /// avaliada e vale o veredicto do avaliador.
    /// </summary>
    public string? MotivoDaInavaliabilidade(Guid regraId) =>
        RegrasInavaliaveis.TryGetValue(regraId, out string? motivo)
            ? ConferenciaDeReferenciasDasRegras.MotivoDaInavaliabilidade(motivo)
            : null;
}
