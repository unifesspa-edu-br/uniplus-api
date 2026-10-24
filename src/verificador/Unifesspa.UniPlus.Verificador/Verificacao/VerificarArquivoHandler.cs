namespace Unifesspa.UniPlus.Verificador.Verificacao;

using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;
using Unifesspa.UniPlus.Verificador.Observabilidade;

/// <summary>
/// Verifica o arquivo do pedido e devolve o veredicto (ADR-0146). O Wolverine publica o retorno na
/// fila de resultados e só confirma o pedido depois disso. É idempotente: a reentrega de um
/// arquivo já promovido devolve o mesmo veredicto sem copiar de novo.
/// </summary>
public sealed partial class VerificarArquivoHandler
{
    private readonly IArmazenamentoDeArquivos _armazenamento;
    private readonly IClamd _clamd;
    private readonly MetricasDoVerificador _metricas;
    private readonly VerificadorOptions _options;
    private readonly ILogger<VerificarArquivoHandler> _logger;

    public VerificarArquivoHandler(
        IArmazenamentoDeArquivos armazenamento,
        IClamd clamd,
        MetricasDoVerificador metricas,
        IOptions<VerificadorOptions> options,
        ILogger<VerificarArquivoHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(clamd);
        ArgumentNullException.ThrowIfNull(metricas);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _armazenamento = armazenamento;
        _clamd = clamd;
        _metricas = metricas;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ResultadoDaVerificacaoDeArquivo> Handle(PedidoDeVerificacaoDeArquivo pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        ResultadoDaVerificacaoDeArquivo resultado;
        try
        {
            resultado = await VerificarAsync(pedido, cancellationToken).ConfigureAwait(false);
        }
        catch (ClamdErroPermanenteException ex)
        {
            // Reprocessar não muda a resposta do clamd, e sem resultado o arquivo ficaria em verificação.
            _metricas.RegistrarErro(OrigemDoErro.Clamd);
            LogFalhaPermanente(_logger, pedido.ArquivoId, ex);
            resultado = Recusa(pedido, MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca);
        }
        catch (DefinitivoSemHashException ex)
        {
            _metricas.RegistrarErro(OrigemDoErro.Minio);
            LogFalhaPermanente(_logger, pedido.ArquivoId, ex);
            resultado = Recusa(pedido, MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca);
        }
        catch (ArmazenamentoException ex)
        {
            _metricas.RegistrarErro(OrigemDoErro.Minio);
            LogFalhaNoArmazenamento(_logger, pedido.ArquivoId, ex);
            throw;
        }
        catch (ClamdException ex)
        {
            _metricas.RegistrarErro(OrigemDoErro.Clamd);
            LogFalhaNoClamd(_logger, pedido.ArquivoId, ex);
            throw;
        }

        _metricas.RegistrarVeredicto(resultado.Veredicto, resultado.Motivo);
        LogVeredicto(_logger, pedido.ArquivoId, resultado.Veredicto, resultado.Motivo);
        return resultado;
    }

    private async Task<ResultadoDaVerificacaoDeArquivo> VerificarAsync(PedidoDeVerificacaoDeArquivo pedido, CancellationToken cancellationToken)
    {
        if (await ResultadoJaPromovidoAsync(pedido, cancellationToken).ConfigureAwait(false) is { } promovido)
        {
            return promovido;
        }

        ObjetoArmazenado? envio = await _armazenamento.ConsultarEnvioAsync(pedido.ChaveDeEnvio, cancellationToken).ConfigureAwait(false);
        if (envio is null || !ArmazenamentoMinio.MesmoETag(envio.ETag, pedido.ETag))
        {
            return await PromovidoOuReenvioAsync(pedido, cancellationToken).ConfigureAwait(false);
        }

        long limite = Math.Min(pedido.TamanhoMaximoBytes, _options.TamanhoMaximoBytes);
        if (envio.TamanhoBytes > limite)
        {
            return Recusa(pedido, MotivoDaRecusa.TamanhoExcedido);
        }

        string diretorio = string.IsNullOrWhiteSpace(_options.DiretorioTemporario) ? Path.GetTempPath() : _options.DiretorioTemporario;
        string caminho = Path.Join(diretorio, $"verificacao-{Guid.NewGuid():N}.tmp");

        // O arquivo temporário vive só durante esta verificação: DeleteOnClose o apaga ao fim, mesmo na falha.
        await using FileStream temporario = new(
            caminho,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        if (!await _armazenamento.LerEnvioAsync(pedido.ChaveDeEnvio, pedido.ETag, temporario, cancellationToken).ConfigureAwait(false))
        {
            return await PromovidoOuReenvioAsync(pedido, cancellationToken).ConfigureAwait(false);
        }

        return await VerificarConteudoAsync(pedido, envio, temporario, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ResultadoDaVerificacaoDeArquivo> VerificarConteudoAsync(
        PedidoDeVerificacaoDeArquivo pedido,
        ObjetoArmazenado envio,
        FileStream conteudo,
        CancellationToken cancellationToken)
    {
        conteudo.Position = 0;
        byte[] inicio = new byte[ConferenciaDoArquivo.BytesDeAssinatura];
        int lidos = await conteudo.ReadAtLeastAsync(inicio, inicio.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (!ConferenciaDoArquivo.FormatoCorresponde(pedido.FormatoDeclarado, inicio.AsSpan(0, lidos)))
        {
            return Recusa(pedido, MotivoDaRecusa.FormatoNaoCorresponde);
        }

        if (pedido.FormatoDeclarado == FormatoDoArquivo.Pdf)
        {
            conteudo.Position = 0;
            if (ConferenciaDoArquivo.PdfExigeSenhaParaAbrir(conteudo))
            {
                return Recusa(pedido, MotivoDaRecusa.PdfComSenha);
            }
        }

        conteudo.Position = 0;
        ResultadoDoClamd analise = await _clamd.VerificarAsync(conteudo, cancellationToken).ConfigureAwait(false);
        if (analise.Infectado)
        {
            LogAmeaca(_logger, pedido.ArquivoId, analise.Assinatura);
            return Recusa(pedido, MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca);
        }

        conteudo.Position = 0;
        string sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(conteudo, cancellationToken).ConfigureAwait(false));

        await _armazenamento.PromoverAsync(
            pedido.ChaveDeEnvio, pedido.ETag, pedido.ChaveDefinitiva, ContentTypeDe(pedido.FormatoDeclarado), sha256, cancellationToken).ConfigureAwait(false);

        await LimparEnvioAsync(pedido, cancellationToken).ConfigureAwait(false);

        return new ResultadoDaVerificacaoDeArquivo(
            pedido.ArquivoId, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, sha256, conteudo.Length, pedido.FormatoDeclarado);
    }

    // O que o candidato mandou no PUT não é confiável: um PDF poliglota com text/html abriria como página.
    private static string ContentTypeDe(FormatoDoArquivo formato) =>
        formato switch
        {
            FormatoDoArquivo.Pdf => "application/pdf",
            FormatoDoArquivo.Jpeg => "image/jpeg",
            FormatoDoArquivo.Png => "image/png",
            _ => "application/octet-stream",
        };

    // O arquivo já está promovido e o veredicto vale; o que sobrar na quarentena expira.
    private async Task LimparEnvioAsync(PedidoDeVerificacaoDeArquivo pedido, CancellationToken cancellationToken)
    {
        try
        {
            await _armazenamento.RemoverEnvioAsync(pedido.ChaveDeEnvio, pedido.ETag, cancellationToken).ConfigureAwait(false);
        }
        catch (ArmazenamentoException ex)
        {
            _metricas.RegistrarErro(OrigemDoErro.Minio);
            LogFalhaNaLimpezaDoEnvio(_logger, pedido.ArquivoId, ex);
        }
    }

    // A versão sumiu do envio: uma entrega duplicada do mesmo pedido pode tê-la promovido e removido no intervalo.
    private async Task<ResultadoDaVerificacaoDeArquivo> PromovidoOuReenvioAsync(PedidoDeVerificacaoDeArquivo pedido, CancellationToken cancellationToken) =>
        await ResultadoJaPromovidoAsync(pedido, cancellationToken).ConfigureAwait(false) ?? Reenvio(pedido);

    private async Task<ResultadoDaVerificacaoDeArquivo?> ResultadoJaPromovidoAsync(PedidoDeVerificacaoDeArquivo pedido, CancellationToken cancellationToken)
    {
        ObjetoArmazenado? definitivo = await _armazenamento.ConsultarDefinitivoAsync(pedido.ChaveDefinitiva, cancellationToken).ConfigureAwait(false);
        if (definitivo is null)
        {
            return null;
        }

        // O definitivo nunca é sobrescrito: se não é desta versão, esta versão não existe mais.
        if (!ArmazenamentoMinio.MesmoETag(definitivo.ETagDoEnvio, pedido.ETag))
        {
            return Reenvio(pedido);
        }

        if (string.IsNullOrEmpty(definitivo.Sha256))
        {
            throw new DefinitivoSemHashException();
        }

        // A entrega anterior pode ter caído antes de remover o envio.
        await LimparEnvioAsync(pedido, cancellationToken).ConfigureAwait(false);
        return new ResultadoDaVerificacaoDeArquivo(
            pedido.ArquivoId, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, definitivo.Sha256, definitivo.TamanhoBytes, pedido.FormatoDeclarado);
    }

    private static ResultadoDaVerificacaoDeArquivo Recusa(PedidoDeVerificacaoDeArquivo pedido, MotivoDaRecusa motivo) =>
        new(pedido.ArquivoId, VeredictoDaVerificacao.Recusado, motivo, null, null, null);

    private static ResultadoDaVerificacaoDeArquivo Reenvio(PedidoDeVerificacaoDeArquivo pedido) =>
        new(pedido.ArquivoId, VeredictoDaVerificacao.ReenvioNecessario, MotivoDaRecusa.NaoSeAplica, null, null, null);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arquivo {ArquivoId} verificado: {Veredicto} ({Motivo})")]
    private static partial void LogVeredicto(ILogger logger, Guid arquivoId, VeredictoDaVerificacao veredicto, MotivoDaRecusa motivo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Arquivo {ArquivoId} recusado pelo antivírus: assinatura {Assinatura}")]
    private static partial void LogAmeaca(ILogger logger, Guid arquivoId, string? assinatura);

    [LoggerMessage(Level = LogLevel.Error, Message = "Arquivo {ArquivoId} não verificado: falha no armazenamento")]
    private static partial void LogFalhaNoArmazenamento(ILogger logger, Guid arquivoId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Arquivo {ArquivoId} recusado por falha permanente da verificação")]
    private static partial void LogFalhaPermanente(ILogger logger, Guid arquivoId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Arquivo {ArquivoId} não verificado: falha no clamd")]
    private static partial void LogFalhaNoClamd(ILogger logger, Guid arquivoId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Arquivo {ArquivoId} promovido, mas o envio não foi removido da quarentena")]
    private static partial void LogFalhaNaLimpezaDoEnvio(ILogger logger, Guid arquivoId, Exception exception);
}

/// <summary>O objeto definitivo da versão confirmada não traz o SHA-256 no metadado.</summary>
public sealed class DefinitivoSemHashException : Exception
{
    public DefinitivoSemHashException()
        : base("O objeto definitivo não traz o SHA-256 no metadado.")
    {
    }

    public DefinitivoSemHashException(string message)
        : base(message)
    {
    }

    public DefinitivoSemHashException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
