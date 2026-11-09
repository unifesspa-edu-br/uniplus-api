namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da proposta de versão; o caso de uso vive em <see cref="PropostasDeVersao"/>.</summary>
public static class CriarVersaoDoPerfilCommandHandler
{
    public static async Task<(Result<ResultadoDaPropostaDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        CriarVersaoDoPerfilCommand command, PropostasDeVersao propostas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(propostas);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await propostas
            .ProporAsync(command.PerfilId, command.Dados, cancellationToken)
            .ConfigureAwait(false);

        return (gravacao.Resultado, gravacao.Avisos);
    }
}
