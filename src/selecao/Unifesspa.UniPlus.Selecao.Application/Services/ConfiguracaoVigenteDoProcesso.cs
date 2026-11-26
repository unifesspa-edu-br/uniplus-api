namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A versão vigente e sua configuração publicada.</summary>
/// <param name="NumeroDaVersao">Número da versão vigente.</param>
/// <param name="Configuracao">A configuração congelada na versão.</param>
/// <param name="TiposDeAto">A classificação, congelada na versão, de cada tipo de ato que o cronograma declara, por código.</param>
internal sealed record ConfiguracaoVigente(int NumeroDaVersao, ConfiguracaoPublicada Configuracao, IReadOnlyDictionary<string, TipoDeAtoCongelado> TiposDeAto);

/// <summary>
/// Lê a configuração congelada que vigora no processo: é onde o produto do edital tem o <c>Id</c> que
/// o ato cumpre, e a configuração em rascunho não entra — a retificação em curso só passa a valer
/// quando publicada.
/// </summary>
public sealed class ConfiguracaoVigenteDoProcesso(
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs)
{
    internal async Task<Result<ConfiguracaoVigente>> LerAsync(
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

    private Result<ConfiguracaoVigente> Reidratar(VersaoConfiguracao versao)
    {
        if (!registroCodecs.LeAVersao(versao.SchemaVersion))
        {
            return Result<ConfiguracaoVigente>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                "A versão do envelope congelado não está entre as capacidades de leitura reconhecidas pelo codec vivo."));
        }

        Result<EnvelopeReidratado> reidratado = registroCodecs.Reidratar(versao);
        return reidratado.IsFailure
            ? Result<ConfiguracaoVigente>.Failure(reidratado.Error!)
            : Result<ConfiguracaoVigente>.Success(new ConfiguracaoVigente(versao.NumeroVersao, reidratado.Value!.Configuracao, reidratado.Value!.DadosDeCatalogo.TiposDeAtoCongelados!));
    }

    // Só quando não há versão vigente se distingue 404 de 422: o caminho comum não paga essa consulta.
    private async Task<Result<ConfiguracaoVigente>> SemVersaoVigenteAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        bool existe = await processoSeletivoRepository.ExisteAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        return Result<ConfiguracaoVigente>.Failure(existe
            ? new DomainError("Snapshot.VigenteAusente", $"Nenhuma publicação vigente para o instante {instante:O}.")
            : AcessoAoProcesso.NaoEncontrado());
    }
}
