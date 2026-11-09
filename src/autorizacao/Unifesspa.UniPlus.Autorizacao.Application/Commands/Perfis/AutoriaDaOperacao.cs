namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Quem executa a operação, pelo token, e o instante em que a executa, pelo relógio injetado.</summary>
public sealed class AutoriaDaOperacao
{
    private readonly IUserContext _userContext;
    private readonly TimeProvider _timeProvider;
    private readonly IConcessaoRepository _concessoes;

    public AutoriaDaOperacao(IUserContext userContext, TimeProvider timeProvider, IConcessaoRepository concessoes)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(concessoes);

        _userContext = userContext;
        _timeProvider = timeProvider;
        _concessoes = concessoes;
    }

    /// <summary>A identidade do token; nulo quando faltam emissor ou sub, e então nada se grava.</summary>
    public Identidade? Autor => IdentidadeDoToken.De(_userContext);

    public DateTimeOffset Agora => _timeProvider.GetUtcNow();

    /// <summary>
    /// Quem pratica o ato sobre uma versão, com o e-mail verificado do token, normalizado, e se tem concessão ativa do
    /// perfil (sem perfil ainda, não tem). Nulo quando o token não identifica a pessoa.
    /// </summary>
    public async Task<AutoriaDaVersao?> DaVersaoAsync(Guid? perfilId, CancellationToken cancellationToken)
    {
        if (Autor is not { } pessoa)
        {
            return null;
        }

        bool temConcessao = perfilId is { } id
            && await _concessoes.TemConcessaoAtivaDoPerfilAsync(pessoa, id, Agora, cancellationToken).ConfigureAwait(false);

        return new AutoriaDaVersao(pessoa, EmailVerificado(), temConcessao);
    }

    private string? EmailVerificado() =>
        _userContext.EmailVerificado && !string.IsNullOrWhiteSpace(_userContext.Email)
            ? _userContext.Email.Trim().ToLowerInvariant()
            : null;
}
