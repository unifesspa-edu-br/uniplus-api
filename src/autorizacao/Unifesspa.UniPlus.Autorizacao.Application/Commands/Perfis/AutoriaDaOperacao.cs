namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Quem executa a operação, pelo token, e o instante em que a executa, pelo relógio injetado.</summary>
public sealed class AutoriaDaOperacao
{
    private readonly IUserContext _userContext;
    private readonly TimeProvider _timeProvider;

    public AutoriaDaOperacao(IUserContext userContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _userContext = userContext;
        _timeProvider = timeProvider;
    }

    /// <summary>A identidade do token; nulo quando faltam emissor ou sub, e então nada se grava.</summary>
    public Identidade? Autor => IdentidadeDoToken.De(_userContext);

    public DateTimeOffset Agora => _timeProvider.GetUtcNow();
}
