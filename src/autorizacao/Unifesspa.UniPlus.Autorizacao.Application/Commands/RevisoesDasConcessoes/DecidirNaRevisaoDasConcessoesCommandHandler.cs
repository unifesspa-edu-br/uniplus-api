namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os handlers das decisões da revisão das concessões; os casos de uso vivem em <see cref="DecisoesDaRevisao"/>.</summary>
public static class DecidirNaRevisaoDasConcessoesCommandHandler
{
    public static async Task<(Result<RevisaoDasConcessoesDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        EnviarDecisoesDaRevisaoCommand command, DecisoesDaRevisao decisoes, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        return Cascata(await decisoes.EnviarDecisoesDaGestaoAsync(command.PerfilId, command.Dados, verificador, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<(Result<RevisaoDasConcessoesDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        DecidirRetidaDaRevisaoCommand command, DecisoesDaRevisao decisoes, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        return Cascata(await decisoes
            .DecidirRetidaAsync(command.PerfilId, command.ConcessaoId, command.Dados, verificador, cancellationToken)
            .ConfigureAwait(false));
    }

    public static async Task<(Result<RevisaoDasConcessoesDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        DecidirEtapaDaProtecaoDaRevisaoCommand command, DecisoesDaRevisao decisoes, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        return Cascata(await decisoes
            .DecidirEtapaDaProtecaoAsync(command.PerfilId, command.Dados, verificador, cancellationToken)
            .ConfigureAwait(false));
    }

    public static async Task<(Result<RevisaoDasConcessoesDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        EncerrarRevisaoDoPerfilCommand command, DecisoesDaRevisao decisoes, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        return Cascata(await decisoes.EncerrarAsync(command.PerfilId, command.Dados, verificador, cancellationToken).ConfigureAwait(false));
    }

    private static (Result<RevisaoDasConcessoesDto> Resposta, IEnumerable<object> Mensagens) Cascata(
        GravacaoDaOperacao<RevisaoDasConcessoesDto> gravacao) =>
        (gravacao.Resultado, gravacao.Avisos);
}
