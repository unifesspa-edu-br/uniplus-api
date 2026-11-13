namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.Diagnostics;
using System.Text.Json;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Grava os registros de acesso publicando, de forma síncrona, no tópico configurado: o acesso só segue depois
/// que o broker confirmou a mensagem.
/// </summary>
/// <remarks>
/// A publicação é direta, sem o outbox do Wolverine, que gravaria na base da aplicação. Cada chamada vira uma única
/// mensagem cujo valor é o lote, de modo que confirmou é o lote inteiro gravado, e não confirmou é nada gravado. A
/// chave da mensagem é a execução do servidor que originou os registros: todos os lotes de uma requisição vão para a
/// mesma partição, na ordem em que foram publicados. Quem consome separa o lote e deduplica por <c>chave</c> de
/// registro. A deduplicação vale para a nova tentativa dentro da mesma execução; uma nova requisição do usuário é um
/// novo acesso, com registros próprios. O valor leva o registro sem o dado, só o código do fato, e o schema dele é o
/// versionado em <c>contracts/kafka/registro-de-acesso</c>.
/// </remarks>
public sealed partial class RegistroDeAcessoKafka : IRegistroOperacionalRestrito, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Func<IProducer<string, string>> _fabrica;
    private readonly Lock _trava = new();
    private readonly TimeProvider _relogio;
    private readonly string _topico;
    private readonly TimeSpan _prazo;
    private IProducer<string, string> _produtor;

    /// <summary>
    /// Cria o produtor com a configuração de <c>RegistroDeAcesso:Kafka</c>. Configuração inválida, inclusive sem
    /// bootstrap servers, lança <see cref="OptionsValidationException"/> ao ler as opções: não há registrador silencioso.
    /// </summary>
    public RegistroDeAcessoKafka(
        IOptions<RegistroDeAcessoKafkaOptions> opcoes, ILogger<RegistroDeAcessoKafka> logger, TimeProvider relogio)
        : this(Fabrica(opcoes, logger), opcoes.Value.Topico, opcoes.Value.PrazoDeConfirmacao, relogio)
    {
    }

    internal RegistroDeAcessoKafka(
        Func<IProducer<string, string>> fabrica, string topico, TimeSpan prazo, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        _fabrica = fabrica;
        _topico = topico;
        _prazo = prazo;
        _relogio = relogio;
        _produtor = fabrica();
    }

    /// <inheritdoc />
    public async Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registros);

        if (registros.Count == 0)
        {
            return;
        }

        // Lote com execuções diferentes é erro de quem o monta, e não falha de gravação: não vira 503.
        Guid execucaoId = ExecucaoDoLote(registros);
        await PublicarAsync(execucaoId, registros, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublicarAsync(
        Guid execucaoId, IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken)
    {
        IProducer<string, string> produtor = ProdutorAtual();

        try
        {
            await produtor.ProduceAsync(_topico, MensagemDoLote(execucaoId, registros), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            RecriarSeFatal(falha, produtor);
            throw new RegistroDeAcessoNaoGravadoException(falha);
        }
    }

    private Message<string, string> MensagemDoLote(Guid execucaoId, IReadOnlyList<RegistroDecisaoAcesso> registros) =>
        new()
        {
            Key = execucaoId.ToString(),
            Value = ValorDoLote(registros),
            Headers = CabecalhosDoRegistroDeAcesso.De(registros, _relogio.GetUtcNow(), Activity.Current),
        };

    private static Guid ExecucaoDoLote(IReadOnlyList<RegistroDecisaoAcesso> registros)
    {
        Guid execucaoId = registros[0].ExecucaoId;

        return registros.All(r => r.ExecucaoId == execucaoId)
            ? execucaoId
            : throw new InvalidOperationException("O lote do registro de acesso reúne registros de execuções diferentes.");
    }

    /// <summary>O valor da mensagem de um lote: o que o consumidor separa, registro a registro.</summary>
    internal static string ValorDoLote(IReadOnlyList<RegistroDecisaoAcesso> registros) =>
        JsonSerializer.Serialize(registros.Select(static r => new EntradaDoLote(r.Chave, r)).ToArray(), Json);

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_trava)
        {
            try
            {
                _produtor.Flush(_prazo);
            }
            finally
            {
                _produtor.Dispose();
            }
        }
    }

    private static Func<IProducer<string, string>> Fabrica(IOptions<RegistroDeAcessoKafkaOptions> opcoes, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);

        // Ler as opções valida a configuração e lança OptionsValidationException se for inválida.
        RegistroDeAcessoKafkaOptions configuracao = opcoes.Value;

        // Sem os dois handlers, os logs do librdkafka vão para o stderr, fora do Serilog e do mascaramento.
        return () => new ProducerBuilder<string, string>(configuracao.ParaProdutor())
            .SetLogHandler((_, mensagem) => RegistrarLogDoProdutor(logger, mensagem))
            .SetErrorHandler((_, erro) => LogErroDoProdutor(logger, erro.Code, erro.Reason, erro.IsFatal))
            .Build();
    }

    private static void RegistrarLogDoProdutor(ILogger logger, LogMessage mensagem)
    {
        LogLevel nivel = ParaLogLevel(mensagem.Level);
        LogLibrdkafka(logger, nivel, mensagem.Facility, mensagem.Message);
    }

    internal static LogLevel ParaLogLevel(SyslogLevel nivel) => nivel switch
    {
        SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical => LogLevel.Critical,
        SyslogLevel.Error => LogLevel.Error,
        SyslogLevel.Warning => LogLevel.Warning,
        SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
        _ => LogLevel.Debug,
    };

    [LoggerMessage(Message = "librdkafka {Facility}: {Mensagem}")]
    private static partial void LogLibrdkafka(ILogger logger, LogLevel level, string facility, string mensagem);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro do produtor do registro de acesso: {Codigo} {Motivo} (fatal: {Fatal})")]
    private static partial void LogErroDoProdutor(ILogger logger, ErrorCode codigo, string motivo, bool fatal);

    private static bool EhFatal(Exception falha) =>
        falha is KafkaException { Error.IsFatal: true } || falha.InnerException is KafkaException { Error.IsFatal: true };

    private IProducer<string, string> ProdutorAtual()
    {
        lock (_trava)
        {
            return _produtor;
        }
    }

    // Após erro fatal o produtor idempotente não volta a funcionar: troca-o para as próximas requisições e descarta o
    // antigo em segundo plano. As chamadas em voo terminam (com erro, e então 503), e a requisição corrente, que responde
    // 503 do mesmo jeito, não espera o descarte.
    private void RecriarSeFatal(Exception falha, IProducer<string, string> falho)
    {
        if (EhFatal(falha) && TrocarProdutor(falho))
        {
            _ = Task.Run(() => Descartar(falho));
        }
    }

    private bool TrocarProdutor(IProducer<string, string> falho)
    {
        lock (_trava)
        {
            // Outra requisição pode já ter trocado o produtor que falhou.
            if (!ReferenceEquals(_produtor, falho))
            {
                return false;
            }

            _produtor = NovoProdutor();
            return true;
        }
    }

    // Falha ao criar o novo produtor também é falha de registro.
    private IProducer<string, string> NovoProdutor()
    {
        try
        {
            return _fabrica();
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            throw new RegistroDeAcessoNaoGravadoException(falha);
        }
    }

    private void Descartar(IProducer<string, string> produtor)
    {
        try
        {
            produtor.Flush(_prazo);
        }
        catch (KafkaException)
        {
            // O produtor já falhou; só interessa liberar os recursos.
        }
        finally
        {
            produtor.Dispose();
        }
    }

    // Um registro do lote com a sua chave, que o consumidor usa para deduplicar.
    private sealed record EntradaDoLote(string Chave, RegistroDecisaoAcesso Registro);
}
