namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem pode revisar a reclassificação: a proteção de dados decide a etapa dela e a gestão de acessos a sua, e qualquer das
/// duas lê o detalhe, assim como quem só consulta as versões. A permissão é conferida antes de qualquer leitura de concessão, e o desfecho que governou vai para a
/// trilha de acesso, permitido ou negado.
/// </summary>
public sealed class AutorizacaoDaReclassificacao
{
    private readonly AutoriaDaOperacao _autoria;

    public AutorizacaoDaReclassificacao(AutoriaDaOperacao autoria)
    {
        ArgumentNullException.ThrowIfNull(autoria);
        _autoria = autoria;
    }

    /// <summary>
    /// Confere a permissão da etapa antes de qualquer leitura da reclassificação: a recusa de acesso é a mesma para o id que
    /// existe e para o que não existe, e a trilha registra o id recebido como recurso. A decisão que governou vai para a trilha, permitida ou negada.
    /// </summary>
    public static async Task<Result> ConferirEtapaAsync(
        Guid reclassificacaoId, EtapaDaVersao etapa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        AcessoDaVersao? acesso = await AcessoDaVersao.ConsultarAsync(verificador, reclassificacaoId, cancellationToken, ResourceTiposAutorizacao.Reclassificacao).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result.Failure(PerfilHandlerErros.AutorNaoIdentificado());
        }

        await AcessoDaVersao.RegistrarAsync(verificador, acesso.DecisaoDa(etapa), cancellationToken).ConfigureAwait(false);

        return acesso.Decide(etapa) ? Result.Success() : Result.Failure(ReclassificacaoErros.SemPermissaoNaEtapa());
    }

    /// <summary>Quem decide a etapa, com o e-mail verificado do token, ou a recusa de identificação.</summary>
    public async Task<Result<AutoriaDaVersao>> AutoriaAsync(Guid perfilId, CancellationToken cancellationToken) =>
        await _autoria.DaVersaoAsync(perfilId, cancellationToken).ConfigureAwait(false) is { } autoria
            ? Result<AutoriaDaVersao>.Success(autoria)
            : Result<AutoriaDaVersao>.Failure(PerfilHandlerErros.AutorNaoIdentificado());

    /// <summary>
    /// Quem lê o detalhe: quem decide alguma das duas etapas ou quem consulta as versões, sem decidir. Devolve a
    /// identidade, ou a recusa.
    /// </summary>
    public async Task<Result<Identidade?>> AutorizarLeituraAsync(
        Guid reclassificacaoId, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        AcessoAvaliado acesso = await AcessoDaVersao.AvaliarLeituraDaReclassificacaoAsync(verificador, reclassificacaoId, cancellationToken).ConfigureAwait(false);

        return acesso.Recusa(ReclassificacaoErros.SemPermissaoNaEtapa) is { } recusa
            ? Result<Identidade?>.Failure(recusa)
            : Result<Identidade?>.Success(_autoria.Autor);
    }
}
