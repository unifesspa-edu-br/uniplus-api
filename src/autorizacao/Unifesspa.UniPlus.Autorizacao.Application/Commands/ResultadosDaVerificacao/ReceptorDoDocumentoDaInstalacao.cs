namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>Aplica o veredicto ao documento de designação da instalação dos gestores.</summary>
public sealed partial class ReceptorDoDocumentoDaInstalacao : IReceptorDoResultadoDaVerificacao
{
    private readonly IInstalacaoRepository _instalacoes;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReceptorDoDocumentoDaInstalacao> _logger;

    public ReceptorDoDocumentoDaInstalacao(
        IInstalacaoRepository instalacoes,
        IAutorizacaoUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger<ReceptorDoDocumentoDaInstalacao> logger)
    {
        ArgumentNullException.ThrowIfNull(instalacoes);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _instalacoes = instalacoes;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DesfechoDoRecebimento> ReceberAsync(ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        Instalacao? instalacao = await _instalacoes.ObterPorIdAsync(resultado.ArquivoId, cancellationToken).ConfigureAwait(false);

        return instalacao is null
            ? DesfechoDoRecebimento.NaoReconhecido
            : await TratarAsync(instalacao, resultado, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DesfechoDoRecebimento> TratarAsync(
        Instalacao instalacao, ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        SituacaoDaInstalacao situacao = instalacao.Situacao;
        if (Registrar(instalacao, resultado))
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            LogResultadoIgnorado(_logger, resultado.ArquivoId, situacao);
        }

        return DesfechoDoRecebimento.Tratado();
    }

    private bool Registrar(Instalacao instalacao, ResultadoDaVerificacaoDeArquivo resultado) =>
        resultado.Veredicto == VeredictoDaVerificacao.Verificado
            ? instalacao.RegistrarDocumentoVerificado(resultado.HashSha256!, _clock.GetUtcNow())
            : instalacao.RegistrarDocumentoRecusado(_clock.GetUtcNow());

    [LoggerMessage(Level = LogLevel.Information, Message = "Resultado da verificação do documento da instalação {InstalacaoId} ignorado: a instalação está {Situacao}")]
    private static partial void LogResultadoIgnorado(ILogger logger, Guid instalacaoId, SituacaoDaInstalacao situacao);
}
