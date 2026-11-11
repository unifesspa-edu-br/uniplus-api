namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem pode ler perfis: a gestão de acessos, que os mantém, e quem aprova pela proteção de dados ou pela gestão de
/// acessos. A recusa vem antes de qualquer leitura de dado.
/// </summary>
internal static class RecusaDaLeituraDoPerfil
{
    /// <summary>
    /// O erro de quem não pode ler o perfil, ou a coleção quando <paramref name="perfilId"/> é nulo; nulo quando pode.
    /// </summary>
    public static async Task<DomainError?> ConferirAsync(IVerificadorDeAcesso verificador, Guid? perfilId, CancellationToken cancellationToken)
    {
        bool? pode = await AcessoDaVersao.PodeLerOPerfilAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);

        return pode switch
        {
            true => null,
            null => PerfilHandlerErros.AutorNaoIdentificado(),
            false => PerfilErros.LeituraNegada(),
        };
    }
}
