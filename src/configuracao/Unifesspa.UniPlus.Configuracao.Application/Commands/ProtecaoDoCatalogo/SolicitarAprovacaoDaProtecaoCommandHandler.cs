namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Envia à proteção de dados os itens pendentes escolhidos; a regra é de <see cref="SolicitacaoDaProtecaoDoCatalogo"/>.
/// O acesso é decidido aqui, item a item, pelo verificador, e não por atributo no endpoint, porque a solicitação alcança
/// os dois cadastros. Os avisos saem como mensagens de saída, no outbox da mesma transação da gravação.
/// </summary>
public static class SolicitarAprovacaoDaProtecaoCommandHandler
{
    public static Task<(Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        SolicitarAprovacaoDaProtecaoCommand command,
        SolicitacaoDaProtecaoDoCatalogo solicitacao,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);

        return solicitacao.SolicitarAsync(command, verificador, AtorDoToken.De(userContext), cancellationToken);
    }
}
