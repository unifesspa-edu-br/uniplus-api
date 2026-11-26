namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Confere se a inscrição está no período no instante de quem chama: a janela vem da versão
/// divulgada, e não da fixada pela inscrição, porque a retificação que prorroga o prazo vale para quem
/// já iniciou.
/// </summary>
public sealed class PeriodoDaInscricaoResolver(
    ICertameDivulgadoRepository certameDivulgadoRepository,
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs,
    IResolvedorFusoInstitucional resolvedorFuso)
{
    /// <summary>Fecha as inscrições, com o motivo, quando o instante está fora do período divulgado ou o período não se confirma.</summary>
    public async Task<InscricoesFechadas?> ConferirAsync(Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        Result<VersaoDivulgada> divulgada = await VersaoDivulgadaResolver
            .ResolverAsync(processoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        return divulgada.IsFailure
            ? PeriodoNaoConfirmado(divulgada.Error!)
            : Conferir(divulgada.Value!.Versao, instante);
    }

    /// <summary>A mesma conferência sobre a versão divulgada já resolvida por quem chama.</summary>
    public InscricoesFechadas? Conferir(VersaoConfiguracao versaoDivulgada,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(resolvedorFuso);

        Result<EnvelopeReidratado> reidratado = ContextoDaVersaoFixadaResolver.Reidratar(versaoDivulgada, registroCodecs);
        return reidratado.IsFailure
            ? PeriodoNaoConfirmado(reidratado.Error!)
            : ConferirNaConfiguracao(reidratado.Value!, instante);
    }

    private InscricoesFechadas? ConferirNaConfiguracao(EnvelopeReidratado envelope, DateTimeOffset instante)
    {
        Result<JanelaDaInscricao> janela = JanelaDaInscricao.Da(envelope.Configuracao, envelope.Dados);
        if (janela.IsFailure)
        {
            return new InscricoesFechadas(MotivoDasInscricoesFechadas.ProcessoSemInscricao, janela.Error!);
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        return fuso.IsFailure
            ? PeriodoNaoConfirmado(fuso.Error!)
            : janela.Value!.Conferir(instante, fuso.Value!);
    }

    private static InscricoesFechadas PeriodoNaoConfirmado(DomainError recusa) =>
        new(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado, recusa);

    /// <summary>A janela de inscrição que a versão divulgada publicou, com o fim que a retificação prorroga.</summary>
    public static Result<JanelaDaInscricao> JanelaDa(VersaoConfiguracao versaoDivulgada, IRegistroCodecsEnvelope registroCodecs)
    {
        ArgumentNullException.ThrowIfNull(versaoDivulgada);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Result<EnvelopeReidratado> reidratado = ContextoDaVersaoFixadaResolver.Reidratar(versaoDivulgada, registroCodecs);

        return reidratado.IsFailure
            ? Result<JanelaDaInscricao>.Failure(reidratado.Error!)
            : JanelaDaInscricao.Da(reidratado.Value!.Configuracao, reidratado.Value!.Dados);
    }
}
