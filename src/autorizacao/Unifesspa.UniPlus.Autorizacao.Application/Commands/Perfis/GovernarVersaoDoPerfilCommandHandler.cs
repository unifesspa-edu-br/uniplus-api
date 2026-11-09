namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os handlers das operações de governança da versão; os casos de uso vivem nas classes que eles chamam.</summary>
public static class GovernarVersaoDoPerfilCommandHandler
{
    public static async Task<(Result<PerfilDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        AprovarVersaoDoPerfilCommand command,
        AprovacoesDaVersao aprovacoes,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(aprovacoes);

        return Cascata(await aprovacoes
            .DecidirAsync(
                new DecisaoSolicitada(command.PerfilId, command.VersaoId, command.Etapa, ResultadoDaDecisao.Aprovado, null),
                verificador,
                cancellationToken)
            .ConfigureAwait(false));
    }

    public static async Task<(Result<PerfilDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        RecusarVersaoDoPerfilCommand command,
        AprovacoesDaVersao aprovacoes,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(aprovacoes);

        return Cascata(await aprovacoes
            .DecidirAsync(
                new DecisaoSolicitada(command.PerfilId, command.VersaoId, command.Etapa, ResultadoDaDecisao.Recusado, command.Motivo),
                verificador,
                cancellationToken)
            .ConfigureAwait(false));
    }

    public static async Task<(Result<PerfilDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        CancelarVersaoDoPerfilCommand command,
        CancelamentoDaVersao cancelamento,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(cancelamento);

        return Cascata(await cancelamento
            .CancelarAsync(command.PerfilId, command.VersaoId, command.Motivo, verificador, cancellationToken)
            .ConfigureAwait(false));
    }

    public static async Task<(Result<PerfilDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        RemoverPermissoesDoPerfilCommand command,
        RemocaoDePermissoes remocao,
        MaterializacaoPrevia previa,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(remocao);
        ArgumentNullException.ThrowIfNull(previa);

        await previa.GarantirAsync(command.PerfilId, cancellationToken).ConfigureAwait(false);

        return Cascata(await remocao.RemoverAsync(command.PerfilId, command.Dados, cancellationToken).ConfigureAwait(false));
    }

    private static (Result<PerfilDto> Resposta, IEnumerable<object> Mensagens) Cascata(GravacaoDaOperacao<PerfilDto> gravacao) =>
        (gravacao.Resultado, gravacao.Avisos);
}
