namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem pode revisar a reclassificação: a proteção de dados decide a etapa dela e a gestão de acessos a sua, e qualquer das
/// duas lê o detalhe. A permissão é conferida antes de qualquer leitura de concessão, e o desfecho que governou vai para a
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

    /// <summary>Quem decide a etapa, com o e-mail verificado do token, ou a recusa de acesso.</summary>
    public async Task<Result<AutoriaDaVersao>> AutorizarAsync(
        Guid perfilId, EtapaDaVersao etapa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        AcessoDaVersao? acesso = await AcessoDaVersao.ConsultarAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<AutoriaDaVersao>.Failure(PerfilHandlerErros.AutorNaoIdentificado());
        }

        await AcessoDaVersao.RegistrarAsync(verificador, acesso.DecisaoDa(etapa), cancellationToken).ConfigureAwait(false);
        if (!acesso.Decide(etapa))
        {
            return Result<AutoriaDaVersao>.Failure(ReclassificacaoErros.SemPermissaoNaEtapa());
        }

        return await _autoria.DaVersaoAsync(perfilId, cancellationToken).ConfigureAwait(false) is { } autoria
            ? Result<AutoriaDaVersao>.Success(autoria)
            : Result<AutoriaDaVersao>.Failure(PerfilHandlerErros.AutorNaoIdentificado());
    }

    /// <summary>Quem lê o detalhe: quem decide alguma das duas etapas. Devolve a identidade, ou a recusa.</summary>
    public async Task<Result<Identidade?>> AutorizarLeituraAsync(Guid perfilId, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        AcessoDaVersao? acesso = await AcessoDaVersao.ConsultarAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<Identidade?>.Failure(PerfilHandlerErros.AutorNaoIdentificado());
        }

        await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);

        return acesso.Aprova ? Result<Identidade?>.Success(_autoria.Autor) : Result<Identidade?>.Failure(ReclassificacaoErros.SemPermissaoNaEtapa());
    }
}
