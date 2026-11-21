namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Services;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que a versão vigente informa ao painel de conformidade: a classificação dos tipos de ato e as exigências que congelou, as
/// fases do cronograma, ou a falha do codec, e o número da versão que nasceria agora.
/// </summary>
internal sealed record VersaoPublicadaNoPainel(
    IReadOnlyDictionary<string, TipoDeAtoCongelado> TiposCongelados,
    CongeladoNaVersaoVigente? CongeladoNaVersao,
    DomainError? Falha,
    int NumeroDaVersaoQueNasce,
    IReadOnlyCollection<FaseCronograma>? Fases);

/// <summary>
/// Lê a versão publicada vigente como o painel a mostra: o que ela congelou e as fases do cronograma dela.
/// </summary>
public sealed class LeituraDaVersaoPublicadaDoPainel(
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs)
{
    // Como a retificação, o checklist trata o tipo que a versão vigente já congelou pela classificação congelada; só o
    // processo ainda não publicado, ou o tipo novo, lê o catálogo de hoje. Uma versão que o codec não lê é recusada pela
    // própria retificação, e o checklist mostra essa recusa como pendência em vez de avaliar os tipos pelo catálogo de
    // hoje e parecer conforme.
    internal async Task<VersaoPublicadaNoPainel> LerAsync(Guid processoId, CancellationToken cancellationToken)
    {
        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository.ObterVersaoAtualAsync(processoId, cancellationToken).ConfigureAwait(false);

        return versaoAtual is null ? SemVersaoPublicada() : Reidratar(versaoAtual);
    }

    private static VersaoPublicadaNoPainel SemVersaoPublicada() =>
        new(new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal), null, null, VersaoConfiguracao.NumeroDaAbertura, null);

    private VersaoPublicadaNoPainel Reidratar(VersaoConfiguracao versaoAtual)
    {
        Result<EnvelopeReidratado> envelope = registroCodecs.Reidratar(versaoAtual);
        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> congelados = CongelamentoDosTiposDeAto.DoEnvelope(envelope);

        return new(
            congelados.IsSuccess ? congelados.Value! : new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal),
            ProtecaoVigenteDasExigenciasDoProcesso.DoEnvelope(envelope),
            congelados.Error,
            VersaoConfiguracao.NumeroDaSucessora(versaoAtual.NumeroVersao),
            envelope.IsSuccess ? envelope.Value!.Configuracao.Cronograma.CronogramaFases : null);
    }
}
