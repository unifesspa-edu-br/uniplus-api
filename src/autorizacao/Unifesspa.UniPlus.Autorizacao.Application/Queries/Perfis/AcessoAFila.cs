namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A recusa de acesso às filas de aprovação, que precede toda leitura: a permissão da etapa pedida governa, e a decisão
/// dela vai para a trilha de acesso, permitida ou negada.
/// </summary>
internal static class AcessoAFila
{
    /// <summary>A recusa para quem não tem a permissão da etapa ou não é identificado pelo token; nulo quando pode listar.</summary>
    public static async Task<DomainError?> NegadoAsync(IVerificadorDeAcesso verificador, EtapaDaVersao etapa, CancellationToken cancellationToken)
    {
        bool? pode = await AcessoDaVersao.PodeListarAsync(verificador, etapa, cancellationToken).ConfigureAwait(false);

        return pode switch
        {
            null => PerfilHandlerErros.AutorNaoIdentificado(),
            false => PerfilErros.SemPermissaoNaEtapa(),
            _ => null,
        };
    }
}
