namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os handlers das decisões da janela de migração; os casos de uso vivem em <see cref="DecisoesDaJanela"/>.</summary>
public static class DecidirNaJanelaCommandHandler
{
    public static async Task<(Result<JanelaDeMigracaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        EnviarLoteDaJanelaCommand command, DecisoesDaJanela decisoes, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        return Cascata(await decisoes.EnviarLoteAsync(command.PerfilId, command.Dados, verificador, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<(Result<JanelaDeMigracaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        DecidirRetidaDaJanelaCommand command, DecisoesDaJanela decisoes, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        return Cascata(await decisoes
            .DecidirRetidaAsync(command.PerfilId, command.ConcessaoId, command.Dados, verificador, cancellationToken)
            .ConfigureAwait(false));
    }

    public static async Task<(Result<JanelaDeMigracaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        DecidirEtapaDaProtecaoDaJanelaCommand command, DecisoesDaJanela decisoes, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        return Cascata(await decisoes
            .DecidirEtapaDaProtecaoAsync(command.PerfilId, command.Dados, verificador, cancellationToken)
            .ConfigureAwait(false));
    }

    public static async Task<(Result<JanelaDeMigracaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        EncerrarJanelaDoPerfilCommand command, DecisoesDaJanela decisoes, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        return Cascata(await decisoes.EncerrarAsync(command.PerfilId, command.Dados, verificador, cancellationToken).ConfigureAwait(false));
    }

    private static (Result<JanelaDeMigracaoDto> Resposta, IEnumerable<object> Mensagens) Cascata(
        GravacaoDaOperacao<JanelaDeMigracaoDto> gravacao) =>
        (gravacao.Resultado, gravacao.Avisos);
}
