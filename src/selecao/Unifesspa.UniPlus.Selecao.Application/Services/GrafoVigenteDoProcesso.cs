namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A versão congelada que vigora e o grafo de configuração dela, já reidratado.</summary>
/// <param name="NumeroDaVersao">Número da versão vigente.</param>
/// <param name="Grafo">A configuração congelada na versão.</param>
/// <param name="TiposDeAto">A classificação, congelada na versão, de cada tipo de ato que o cronograma declara, por código.</param>
internal sealed record GrafoVigente(int NumeroDaVersao, GrafoConfiguracao Grafo, IReadOnlyDictionary<string, TipoDeAtoCongelado> TiposDeAto);

/// <summary>
/// Lê a configuração congelada que vigora no processo: é onde o produto do edital tem o <c>Id</c> que
/// o ato cumpre, e a configuração em rascunho não entra — a retificação em curso só passa a valer
/// quando publicada.
/// </summary>
public sealed class GrafoVigenteDoProcesso(
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs)
{
    internal async Task<Result<GrafoVigente>> LerAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        VersaoConfiguracao? versao = await processoSeletivoRepository
            .ObterVersaoVigenteAsync(processoSeletivoId, instante, cancellationToken)
            .ConfigureAwait(false);
        if (versao is null)
        {
            return await SemVersaoVigenteAsync(processoSeletivoId, instante, cancellationToken)
                .ConfigureAwait(false);
        }

        return Reidratar(versao);
    }

    private Result<GrafoVigente> Reidratar(VersaoConfiguracao versao)
    {
        if (!registroCodecs.SabeLer(versao.SchemaVersion))
        {
            return Result<GrafoVigente>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                "A versão do envelope congelado não está entre as capacidades de leitura reconhecidas pelo codec vivo."));
        }

        Result<EnvelopeReidratado> reidratado = registroCodecs.Reidratar(versao);
        return reidratado.IsFailure
            ? Result<GrafoVigente>.Failure(reidratado.Error!)
            : Result<GrafoVigente>.Success(new GrafoVigente(versao.NumeroVersao, reidratado.Value!.Grafo, reidratado.Value!.TiposDeAtoCongelados));
    }

    // Só quando não há versão vigente se distingue 404 de 422: o caminho comum não paga essa consulta.
    private async Task<Result<GrafoVigente>> SemVersaoVigenteAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        bool existe = await processoSeletivoRepository.ExisteAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        return Result<GrafoVigente>.Failure(existe
            ? new DomainError("Snapshot.VigenteAusente", $"Nenhuma publicação vigente para o instante {instante:O}.")
            : new DomainError("ProcessoSeletivo.NaoEncontrado", "Processo seletivo não encontrado."));
    }
}
