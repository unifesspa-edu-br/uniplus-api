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
internal static class GrafoVigenteDoProcesso
{
    public static async Task<Result<GrafoVigente>> LerAsync(
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        VersaoConfiguracao? versao = await processoSeletivoRepository
            .ObterVersaoVigenteAsync(processoSeletivoId, instante, cancellationToken)
            .ConfigureAwait(false);
        if (versao is null)
        {
            return await SemVersaoVigenteAsync(processoSeletivoId, instante, processoSeletivoRepository, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!registroCodecs.SabeLer(versao.SchemaVersion))
        {
            return Result<GrafoVigente>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                $"A versão '{versao.SchemaVersion}' do envelope congelado não está entre as capacidades de leitura reconhecidas pelo codec vivo."));
        }

        Result<EnvelopeReidratado> reidratado = registroCodecs.Reidratar(versao);
        return reidratado.IsFailure
            ? Result<GrafoVigente>.Failure(reidratado.Error!)
            : Result<GrafoVigente>.Success(new GrafoVigente(versao.NumeroVersao, reidratado.Value!.Grafo, reidratado.Value!.TiposDeAtoCongelados));
    }

    // Só quando não há versão vigente se distingue 404 de 422: o caminho comum não paga essa consulta.
    private static async Task<Result<GrafoVigente>> SemVersaoVigenteAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        bool existe = await processoSeletivoRepository.ExisteAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        return Result<GrafoVigente>.Failure(existe
            ? new DomainError("Snapshot.VigenteAusente", $"Nenhuma publicação vigente para o instante {instante:O}.")
            : new DomainError("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {processoSeletivoId} não encontrado."));
    }
}
