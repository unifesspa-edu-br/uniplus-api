namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem pode decidir na janela de migração: a gestão de acessos decide o lote, a retida e o encerramento, e a proteção
/// de dados decide a etapa dela. A permissão é conferida antes de qualquer leitura de concessão, e a decisão que governou
/// vai para a trilha de acesso, permitida ou negada.
/// </summary>
public sealed class AutorizacaoDaMigracao
{
    private readonly AutoriaDaOperacao _autoria;

    public AutorizacaoDaMigracao(AutoriaDaOperacao autoria)
    {
        ArgumentNullException.ThrowIfNull(autoria);
        _autoria = autoria;
    }

    /// <summary>Quem pratica o ato, com o e-mail verificado do token, ou a recusa de acesso.</summary>
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
            return Result<AutoriaDaVersao>.Failure(MigracaoErros.SemPermissaoNaEtapa());
        }

        return await _autoria.DaVersaoAsync(perfilId, cancellationToken).ConfigureAwait(false) is { } autoria
            ? Result<AutoriaDaVersao>.Success(autoria)
            : Result<AutoriaDaVersao>.Failure(PerfilHandlerErros.AutorNaoIdentificado());
    }
}
