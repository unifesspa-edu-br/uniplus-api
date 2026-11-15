namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.Diagnostics;

using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using unifesspa.uniplus.autorizacao.registrodeacesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;

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
/// embutido em <c>Authorization/Contracts/Schemas</c>.
/// </remarks>
public sealed partial class RegistroDeAcessoKafka : IRegistroOperacionalRestrito, IDisposable
{
    private readonly Func<IProducer<string, byte[]>> _fabrica;
    private readonly IAsyncSerializer<LoteDoRegistroDeAcesso> _serializador;
    private readonly Lock _trava = new();
    private readonly SemaphoreSlim _vagaDeSerializacao = new(1, 1);
    private readonly TimeProvider _relogio;
    private readonly string _topico;
    private readonly TimeSpan _prazo;
    private IProducer<string, byte[]> _produtor;

    /// <summary>
    /// Cria o produtor com a configuração de <c>RegistroDeAcesso:Kafka</c>. Configuração inválida, inclusive sem
    /// bootstrap servers, lança <see cref="OptionsValidationException"/> ao ler as opções: não há registrador silencioso.
    /// </summary>
    public RegistroDeAcessoKafka(
        IOptions<RegistroDeAcessoKafkaOptions> opcoes,
        ILogger<RegistroDeAcessoKafka> logger,
        TimeProvider relogio,
        ISchemaRegistryClient schemaRegistry)
        : this(
            Fabrica(opcoes, logger),
            SerializadorDoValor(schemaRegistry),
            opcoes.Value.Topico,
            opcoes.Value.PrazoDeConfirmacao,
            relogio)
    {
    }

    internal RegistroDeAcessoKafka(
        Func<IProducer<string, byte[]>> fabrica,
        IAsyncSerializer<LoteDoRegistroDeAcesso> serializador,
        string topico,
        TimeSpan prazo,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        _fabrica = fabrica;
        _serializador = serializador;
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

    // O produtor é obtido só depois da serialização, que pode esperar: outra requisição pode ter trocado o produtor nesse
    // meio tempo, e o lote deve ir ao atual.
    private async Task PublicarAsync(
        Guid execucaoId, IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken)
    {
        Message<string, byte[]> mensagem = await MensagemOuFalhaAsync(execucaoId, registros, cancellationToken).ConfigureAwait(false);

        IProducer<string, byte[]> produtor = ProdutorAtual();

        try
        {
            await produtor.ProduceAsync(_topico, mensagem, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            RecriarSeFatal(falha, produtor);
            throw new RegistroDeAcessoNaoGravadoException(falha);
        }
    }

    private async Task<Message<string, byte[]>> MensagemOuFalhaAsync(
        Guid execucaoId, IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken)
    {
        try
        {
            Message<string, byte[]> mensagem = await MensagemDoLoteAsync(execucaoId, registros, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            return mensagem;
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            throw new RegistroDeAcessoNaoGravadoException(falha);
        }
    }

    private async Task<Message<string, byte[]>> MensagemDoLoteAsync(
        Guid execucaoId, IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
        new()
        {
            Key = execucaoId.ToString(),
            Value = await SerializarAsync(MapeadorDoRegistroDeAcesso.ParaLote(registros), cancellationToken).ConfigureAwait(false),
            Headers = CabecalhosDoRegistroDeAcesso.De(registros, _relogio.GetUtcNow(), Activity.Current),
        };

    // O lote é serializado antes de ir ao produtor, e a serialização tem o prazo de confirmação do broker como limite
    // próprio: o serializador consulta o Schema Registry, que fora do ar retentaria por muito mais tempo, e a requisição
    // esperaria esse tempo. Cada etapa é limitada ao prazo, então no pior caso a requisição espera até o dobro dele.
    // Falha ou prazo vencido aqui é lote que nunca chegou ao produtor, portanto nada gravado. O serializador não aceita
    // cancelamento, então uma só serialização fica em voo por vez, e quem espera a vez desiste no prazo, ou quando a
    // requisição é cancelada, sem produzir o lote: com o Schema Registry fora, o trabalho abandonado não se acumula, e
    // quando ele volta a primeira chamada libera a vez.
    private async Task<byte[]> SerializarAsync(LoteDoRegistroDeAcesso lote, CancellationToken cancellationToken)
    {
        using CancellationTokenSource limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(_prazo);

        try
        {
            await _vagaDeSerializacao.WaitAsync(limite.Token).ConfigureAwait(false);

            return await SerializarLiberandoAVagaAsync(lote).WaitAsync(limite.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException esgotado) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"O lote do registro de acesso não foi serializado em {_prazo}.", esgotado);
        }
    }

    private async Task<byte[]> SerializarLiberandoAVagaAsync(LoteDoRegistroDeAcesso lote)
    {
        try
        {
            return await _serializador
                .SerializeAsync(lote, new SerializationContext(MessageComponentType.Value, _topico))
                .ConfigureAwait(false);
        }
        finally
        {
            _vagaDeSerializacao.Release();
        }
    }

    private static Guid ExecucaoDoLote(IReadOnlyList<RegistroDecisaoAcesso> registros)
    {
        Guid execucaoId = registros[0].ExecucaoId;

        return registros.All(r => r.ExecucaoId == execucaoId)
            ? execucaoId
            : throw new InvalidOperationException("O lote do registro de acesso reúne registros de execuções diferentes.");
    }

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
                _vagaDeSerializacao.Dispose();
            }
        }
    }

    private static Func<IProducer<string, byte[]>> Fabrica(IOptions<RegistroDeAcessoKafkaOptions> opcoes, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);

        // Ler as opções valida a configuração e lança OptionsValidationException se for inválida.
        RegistroDeAcessoKafkaOptions configuracao = opcoes.Value;

        // Sem os dois handlers, os logs do librdkafka vão para o stderr, fora do Serilog e do mascaramento.
        return () => new ProducerBuilder<string, byte[]>(configuracao.ParaProdutor())
            .SetLogHandler((_, mensagem) => RegistrarLogDoProdutor(logger, mensagem))
            .SetErrorHandler((_, erro) => LogErroDoProdutor(logger, erro.Code, erro.Reason, erro.IsFatal))
            .Build();
    }

    // O esquema vai ao Schema Registry na subida; se a API subiu sem ele, o serializador registra o mesmo esquema, de
    // forma idempotente, no primeiro lote, e o registro de acesso se recupera quando o Schema Registry volta (ADR-0051).
    private static AvroSerializer<LoteDoRegistroDeAcesso> SerializadorDoValor(ISchemaRegistryClient schemaRegistry)
    {
        ArgumentNullException.ThrowIfNull(schemaRegistry);

        return new AvroSerializer<LoteDoRegistroDeAcesso>(
            schemaRegistry, new AvroSerializerConfig { AutoRegisterSchemas = true, NormalizeSchemas = true });
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

    private IProducer<string, byte[]> ProdutorAtual()
    {
        lock (_trava)
        {
            return _produtor;
        }
    }

    // Após erro fatal o produtor idempotente não volta a funcionar: troca-o para as próximas requisições e descarta o
    // antigo em segundo plano. As chamadas em voo terminam (com erro, e então 503), e a requisição corrente, que responde
    // 503 do mesmo jeito, não espera o descarte.
    private void RecriarSeFatal(Exception falha, IProducer<string, byte[]> falho)
    {
        if (EhFatal(falha) && TrocarProdutor(falho))
        {
            _ = Task.Run(() => Descartar(falho));
        }
    }

    private bool TrocarProdutor(IProducer<string, byte[]> falho)
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
    private IProducer<string, byte[]> NovoProdutor()
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

    private void Descartar(IProducer<string, byte[]> produtor)
    {
        using IProducer<string, byte[]> descartavel = produtor;

        try
        {
            descartavel.Flush(_prazo);
        }
        catch (KafkaException)
        {
            // O produtor já falhou; só interessa liberar os recursos.
        }
    }

    // Um registro do lote com a sua chave, que o consumidor usa para deduplicar.
    private sealed record EntradaDoLote(string Chave, RegistroDecisaoAcesso Registro);
}
