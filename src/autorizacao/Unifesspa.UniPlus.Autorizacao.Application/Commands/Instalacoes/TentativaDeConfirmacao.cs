namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A conferência do código da confirmação. Todas as recusas de código têm o mesmo código de erro e a mesma mensagem, para
/// não revelar se há instalação pendente. A tentativa é gravada na mesma transação que a decide, inclusive quando a
/// resposta é recusa.
/// </summary>
public sealed partial class TentativaDeConfirmacao
{
    private readonly IInstalacaoRepository _instalacoes;
    private readonly IResumoDeCodigoDeConfirmacao _resumo;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly ILogger<TentativaDeConfirmacao> _logger;

    public TentativaDeConfirmacao(
        IInstalacaoRepository instalacoes,
        IResumoDeCodigoDeConfirmacao resumo,
        IAutorizacaoUnitOfWork unitOfWork,
        ILogger<TentativaDeConfirmacao> logger)
    {
        ArgumentNullException.ThrowIfNull(instalacoes);
        ArgumentNullException.ThrowIfNull(resumo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _instalacoes = instalacoes;
        _resumo = resumo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// A instalação pendente de quem confirma, com o código certo e a tentativa aceita, ou a recusa já gravada. Quem tenta
    /// sem ter instalação aguardando código não toca a pendência alheia nem consome as tentativas de ninguém: o fato fica
    /// registrado e logado com o <c>sub</c> de quem tentou.
    /// </summary>
    internal async Task<Result<Instalacao>> ConferirAsync(
        Identidade autor, string? codigo, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Instalacao? instalacao = await _instalacoes.ObterAguardandoCodigoDoExecutorAsync(autor, cancellationToken).ConfigureAwait(false);
        if (instalacao is null)
        {
            LogTentativaSemPendencia(_logger, autor.Sub);
            return await RecusarAsync(null, autor, ResultadoDaTentativaDeInstalacao.SemPendencia, agora, cancellationToken).ConfigureAwait(false);
        }

        bool codigoConfere = await CodigoConfereAsync(instalacao, codigo, cancellationToken).ConfigureAwait(false);
        ResultadoDaTentativaDeInstalacao resultado = instalacao.RegistrarTentativa(codigoConfere, agora);

        return resultado == ResultadoDaTentativaDeInstalacao.Aceita
            ? Result<Instalacao>.Success(instalacao)
            : await RecusarAsync(instalacao.Id, autor, resultado, agora, cancellationToken).ConfigureAwait(false);
    }

    // Comparação em tempo constante. O código sem a forma de código nem chega ao cálculo do resumo.
    private async Task<bool> CodigoConfereAsync(Instalacao instalacao, string? codigo, CancellationToken cancellationToken)
    {
        string informado = codigo?.Trim() ?? string.Empty;
        if (!CodigoDeConfirmacao.TemAForma(informado) || instalacao.CodigoResumo is not { } gravado)
        {
            return false;
        }

        byte[] calculado = await _resumo.ResumirAsync(instalacao.Id, informado, cancellationToken).ConfigureAwait(false);

        return CryptographicOperations.FixedTimeEquals(calculado, gravado);
    }

    // A leitura trava a instalação, então só outra operação sobre a mesma linha fora desta trava falha ao gravar.
    private async Task<Result<Instalacao>> RecusarAsync(
        Guid? instalacaoId, Identidade autor, ResultadoDaTentativaDeInstalacao resultado, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        await _instalacoes.AdicionarTentativaAsync(TentativaDeInstalacao.Registrar(instalacaoId, autor, resultado, agora), cancellationToken)
            .ConfigureAwait(false);

        DomainError? falha = await GravacaoDaConfirmacao.GravarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);

        return Result<Instalacao>.Failure(falha ?? InstalacaoHandlerErros.CodigoInvalido());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tentativa de confirmar a instalação sem instalação aguardando código: sub {Sub}")]
    private static partial void LogTentativaSemPendencia(ILogger logger, string sub);
}
