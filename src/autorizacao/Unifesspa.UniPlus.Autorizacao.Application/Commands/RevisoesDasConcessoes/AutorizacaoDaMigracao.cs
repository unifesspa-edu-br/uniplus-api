namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem pode decidir na revisão das concessões: a gestão de acessos decide o envio de decisões, a retida e o encerramento, e a proteção
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

        DomainError? recusa = await AcessoDaVersao
            .ConferirEtapaAsync(verificador, perfilId, ResourceTiposAutorizacao.Perfil, etapa, RevisaoDasConcessoesErros.SemPermissaoNaEtapa(), cancellationToken)
            .ConfigureAwait(false);

        return recusa is null
            ? await _autoria.ExigirDaVersaoAsync(perfilId, cancellationToken).ConfigureAwait(false)
            : Result<AutoriaDaVersao>.Failure(recusa);
    }
}
