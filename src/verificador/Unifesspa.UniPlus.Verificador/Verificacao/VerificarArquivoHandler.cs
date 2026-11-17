namespace Unifesspa.UniPlus.Verificador.Verificacao;

using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;
using Unifesspa.UniPlus.Verificador.Observabilidade;

using Wolverine;

/// <summary>
/// Verifica o arquivo do pedido e publica o veredicto na fila de resultados do destino (ADR-0146). O pedido só é
/// confirmado depois de a publicação ser confirmada. É idempotente: a reentrega de um
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

    /// <summary>
    /// Verifica o arquivo e publica o resultado na fila do destino do pedido, antes de o pedido ser confirmado.
    /// </summary>
    public async Task Handle(PedidoDeVerificacaoDeArquivo pedido, IMessageBus bus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(bus);

        // O destino é um conjunto fechado: um valor fora dele é defeito do pedido e vai para a fila de erro, sem
        // inventar uma fila.
        if (!Enum.IsDefined(pedido.Destino))
        {
            throw new InvalidOperationException($"O pedido do arquivo {pedido.ArquivoId} traz um destino de resultado desconhecido ({(int)pedido.Destino}).");
        }

        ResultadoDaVerificacaoDeArquivo resultado = await Verificar(pedido, cancellationToken).ConfigureAwait(false);
        await bus.EndpointFor(FilasDaVerificacao.ResultadosDe(pedido.Destino)).SendAsync(resultado).ConfigureAwait(false);
    }

    public async Task<ResultadoDaVerificacaoDeArquivo> Verificar(PedidoDeVerificacaoDeArquivo pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        ResultadoDaVerificacaoDeArquivo resultado = await VerificarRecusandoFalhaPermanenteAsync(pedido, cancellationToken).ConfigureAwait(false);
        _metricas.RegistrarVeredicto(resultado.Veredicto, resultado.Motivo);
        LogVeredicto(_logger, pedido.ArquivoId, resultado.Veredicto, resultado.Motivo);
        return resultado;
    }

    // Falha permanente recusa o arquivo; falha transitória propaga para a política de erro tentar de novo. O erro
    // permanente do clamd é um ClamdException, por isso o filtro dele vem antes do genérico.
    private async Task<ResultadoDaVerificacaoDeArquivo> VerificarRecusandoFalhaPermanenteAsync(PedidoDeVerificacaoDeArquivo pedido, CancellationToken cancellationToken)
    {
        try
        {
            return await VerificarAsync(pedido, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ClamdErroPermanenteException or DefinitivoSemHashException)
        {
            return RecusarPorFalhaPermanente(pedido, OrigemDe(ex), ex);
        }
        catch (Exception ex) when (ex is ArmazenamentoException or ClamdException)
        {
            RegistrarFalhaTransitoria(pedido, OrigemDe(ex), ex);
            throw;
        }
    }

    private static OrigemDoErro OrigemDe(Exception falha) =>
        falha is ArmazenamentoException or DefinitivoSemHashException ? OrigemDoErro.Minio : OrigemDoErro.Clamd;

    // Reprocessar não muda a resposta do clamd, e sem resultado o arquivo ficaria em verificação.
    private ResultadoDaVerificacaoDeArquivo RecusarPorFalhaPermanente(PedidoDeVerificacaoDeArquivo pedido, OrigemDoErro origem, Exception falha)
    {
        _metricas.RegistrarErro(origem);
        LogFalhaPermanente(_logger, pedido.ArquivoId, falha);
        return Recusa(pedido, MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca);
    }

    private void RegistrarFalhaTransitoria(PedidoDeVerificacaoDeArquivo pedido, OrigemDoErro origem, Exception falha)
    {
        _metricas.RegistrarErro(origem);
        LogFalhaTransitoria(_logger, pedido.ArquivoId, origem, falha);
    }

    private async Task<ResultadoDaVerificacaoDeArquivo> VerificarAsync(PedidoDeVerificacaoDeArquivo pedido, CancellationToken cancellationToken)
    {
        if (await ResultadoJaPromovidoAsync(pedido, cancellationToken).ConfigureAwait(false) is { } promovido)
        {
            return promovido;
        }

        ObjetoArmazenado? envio = await _armazenamento.ConsultarEnvioAsync(pedido.ChaveDeEnvio, cancellationToken).ConfigureAwait(false);
        return envio is null || !ArmazenamentoMinio.MesmoETag(envio.ETag, pedido.ETag)
            ? await PromovidoOuReenvioAsync(pedido, cancellationToken).ConfigureAwait(false)
            : await VerificarEnvioAsync(pedido, envio, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ResultadoDaVerificacaoDeArquivo> VerificarEnvioAsync(
        PedidoDeVerificacaoDeArquivo pedido,
        ObjetoArmazenado envio,
        CancellationToken cancellationToken)
    {
        if (envio.TamanhoBytes > Math.Min(pedido.TamanhoMaximoBytes, _options.TamanhoMaximoBytes))
        {
            return Recusa(pedido, MotivoDaRecusa.TamanhoExcedido);
        }

        FileStream temporario = AbrirTemporario();
        await using (temporario.ConfigureAwait(false))
        {
            return await _armazenamento.LerEnvioAsync(pedido.ChaveDeEnvio, pedido.ETag, temporario, cancellationToken).ConfigureAwait(false)
                ? await VerificarConteudoAsync(pedido, temporario, cancellationToken).ConfigureAwait(false)
                : await PromovidoOuReenvioAsync(pedido, cancellationToken).ConfigureAwait(false);
        }
    }

    // O arquivo temporário vive só durante esta verificação: DeleteOnClose o apaga ao fim, mesmo na falha.
    private FileStream AbrirTemporario()
    {
        string diretorio = string.IsNullOrWhiteSpace(_options.DiretorioTemporario) ? Path.GetTempPath() : _options.DiretorioTemporario;
        return new FileStream(
            Path.Join(diretorio, $"verificacao-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
    }

    private async Task<ResultadoDaVerificacaoDeArquivo> VerificarConteudoAsync(
        PedidoDeVerificacaoDeArquivo pedido,
        FileStream conteudo,
        CancellationToken cancellationToken)
    {
        MotivoDaRecusa? motivo = await MotivoDeRecusaDoConteudoAsync(pedido, conteudo, cancellationToken).ConfigureAwait(false);
        return motivo is { } recusa
            ? Recusa(pedido, recusa)
            : await PromoverAsync(pedido, conteudo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MotivoDaRecusa?> MotivoDeRecusaDoConteudoAsync(PedidoDeVerificacaoDeArquivo pedido, FileStream conteudo, CancellationToken cancellationToken)
    {
        if (!await FormatoCorrespondeAsync(pedido, conteudo, cancellationToken).ConfigureAwait(false))
        {
            return MotivoDaRecusa.FormatoNaoCorresponde;
        }

        if (ExigeSenhaParaAbrir(pedido, conteudo))
        {
            return MotivoDaRecusa.PdfComSenha;
        }

        return await PassouNoAntivirusAsync(pedido, conteudo, cancellationToken).ConfigureAwait(false)
            ? null
            : MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca;
    }

    private static async Task<bool> FormatoCorrespondeAsync(PedidoDeVerificacaoDeArquivo pedido, FileStream conteudo, CancellationToken cancellationToken)
    {
        conteudo.Position = 0;
        byte[] inicio = new byte[ConferenciaDoArquivo.BytesDeAssinatura];
        int lidos = await conteudo.ReadAtLeastAsync(inicio, inicio.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        return ConferenciaDoArquivo.FormatoCorresponde(pedido.FormatoDeclarado, inicio.AsSpan(0, lidos));
    }

    private static bool ExigeSenhaParaAbrir(PedidoDeVerificacaoDeArquivo pedido, FileStream conteudo)
    {
        if (pedido.FormatoDeclarado != FormatoDoArquivo.Pdf)
        {
            return false;
        }

        conteudo.Position = 0;
        return ConferenciaDoArquivo.PdfExigeSenhaParaAbrir(conteudo);
    }

    private async Task<bool> PassouNoAntivirusAsync(PedidoDeVerificacaoDeArquivo pedido, FileStream conteudo, CancellationToken cancellationToken)
    {
        conteudo.Position = 0;
        ResultadoDoClamd analise = await _clamd.VerificarAsync(conteudo, cancellationToken).ConfigureAwait(false);
        if (analise.Infectado)
        {
            LogAmeaca(_logger, pedido.ArquivoId, analise.Assinatura);
        }

        return !analise.Infectado;
    }

    private async Task<ResultadoDaVerificacaoDeArquivo> PromoverAsync(PedidoDeVerificacaoDeArquivo pedido, FileStream conteudo, CancellationToken cancellationToken)
    {
        conteudo.Position = 0;
        string sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(conteudo, cancellationToken).ConfigureAwait(false));

        await _armazenamento.PromoverAsync(
            pedido.ChaveDeEnvio, pedido.ETag, pedido.ChaveDefinitiva, ContentTypeDe(pedido.FormatoDeclarado), sha256, cancellationToken).ConfigureAwait(false);

        await LimparEnvioAsync(pedido, cancellationToken).ConfigureAwait(false);
        return Verificado(pedido, sha256, conteudo.Length);
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
        return definitivo is null
            ? null
            : await ResultadoDoDefinitivoAsync(pedido, definitivo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ResultadoDaVerificacaoDeArquivo> ResultadoDoDefinitivoAsync(
        PedidoDeVerificacaoDeArquivo pedido,
        ObjetoArmazenado definitivo,
        CancellationToken cancellationToken)
    {
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
        return Verificado(pedido, definitivo.Sha256, definitivo.TamanhoBytes);
    }

    private static ResultadoDaVerificacaoDeArquivo Verificado(PedidoDeVerificacaoDeArquivo pedido, string sha256, long tamanhoBytes) =>
        new(pedido.ArquivoId, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, sha256, tamanhoBytes, pedido.FormatoDeclarado);

    private static ResultadoDaVerificacaoDeArquivo Recusa(PedidoDeVerificacaoDeArquivo pedido, MotivoDaRecusa motivo) =>
        new(pedido.ArquivoId, VeredictoDaVerificacao.Recusado, motivo, null, null, null);

    private static ResultadoDaVerificacaoDeArquivo Reenvio(PedidoDeVerificacaoDeArquivo pedido) =>
        new(pedido.ArquivoId, VeredictoDaVerificacao.ReenvioNecessario, MotivoDaRecusa.NaoSeAplica, null, null, null);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arquivo {ArquivoId} verificado: {Veredicto} ({Motivo})")]
    private static partial void LogVeredicto(ILogger logger, Guid arquivoId, VeredictoDaVerificacao veredicto, MotivoDaRecusa motivo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Arquivo {ArquivoId} recusado pelo antivírus: assinatura {Assinatura}")]
    private static partial void LogAmeaca(ILogger logger, Guid arquivoId, string? assinatura);

    [LoggerMessage(Level = LogLevel.Error, Message = "Arquivo {ArquivoId} não verificado: falha transitória ({Origem})")]
    private static partial void LogFalhaTransitoria(ILogger logger, Guid arquivoId, OrigemDoErro origem, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Arquivo {ArquivoId} recusado por falha permanente da verificação")]
    private static partial void LogFalhaPermanente(ILogger logger, Guid arquivoId, Exception exception);

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
