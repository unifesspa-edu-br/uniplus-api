namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Aplica o veredicto ao documento de uma versão de perfil. A gravação fica com <see cref="ResultadoDoDocumentoDaVersao"/>,
/// que a faz sob a trava do perfil; aqui só se traduz o desfecho.
/// </summary>
public sealed partial class ReceptorDoDocumentoDaVersao : IReceptorDoResultadoDaVerificacao
{
    private readonly ResultadoDoDocumentoDaVersao _versoes;
    private readonly ILogger<ReceptorDoDocumentoDaVersao> _logger;

    public ReceptorDoDocumentoDaVersao(ResultadoDoDocumentoDaVersao versoes, ILogger<ReceptorDoDocumentoDaVersao> logger)
    {
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(logger);

        _versoes = versoes;
        _logger = logger;
    }

    public async Task<DesfechoDoRecebimento> ReceberAsync(ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        DesfechoDoResultadoDaVersao desfecho = await _versoes.RegistrarAsync(resultado, cancellationToken).ConfigureAwait(false);
        if (desfecho == DesfechoDoResultadoDaVersao.Desconhecido)
        {
            return DesfechoDoRecebimento.NaoReconhecido;
        }

        if (desfecho == DesfechoDoResultadoDaVersao.Ignorado)
        {
            LogDocumentoIgnorado(_logger, resultado.ArquivoId);
        }

        return DesfechoDoRecebimento.Tratado();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resultado da verificação do documento da versão {DocumentoId} ignorado: o documento não esperava veredicto")]
    private static partial void LogDocumentoIgnorado(ILogger logger, Guid documentoId);
}
