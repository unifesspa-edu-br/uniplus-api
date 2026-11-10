namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da decisão da reclassificação; o caso de uso vive em <see cref="DecisoesDaReclassificacao"/>.</summary>
public static class DecidirReclassificacaoCommandHandler
{
    public static async Task<(Result<DecisaoDaReclassificacaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        DecidirReclassificacaoCommand command,
        DecisoesDaReclassificacao decisoes,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decisoes);

        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> gravacao = await decisoes
            .DecidirAsync(command.ReclassificacaoId, command.Dados, verificador, cancellationToken)
            .ConfigureAwait(false);

        return (gravacao.Resultado, gravacao.Avisos);
    }
}
