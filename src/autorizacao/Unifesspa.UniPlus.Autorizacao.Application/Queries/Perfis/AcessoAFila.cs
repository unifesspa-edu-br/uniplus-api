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
        AcessoAvaliado acesso = await AcessoDaVersao.AvaliarListagemAsync(verificador, etapa, cancellationToken).ConfigureAwait(false);

        return acesso.Recusa(PerfilErros.SemPermissaoNaEtapa);
    }

    /// <summary>
    /// Lê a etapa pedida e confere o acesso a ela: a etapa desconhecida é recusada antes de qualquer registro, e a decisão
    /// da permissão da etapa vai para a trilha.
    /// </summary>
    public static async Task<Result<EtapaDaVersao>> AutorizarAsync(string? etapa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        if (EtapaDaVersaoCodigo.TentarLer(etapa) is not { } pedida)
        {
            return Result<EtapaDaVersao>.Failure(PerfilErros.EtapaInvalida());
        }

        DomainError? negado = await NegadoAsync(verificador, pedida, cancellationToken).ConfigureAwait(false);

        return negado is null ? Result<EtapaDaVersao>.Success(pedida) : Result<EtapaDaVersao>.Failure(negado);
    }
}
