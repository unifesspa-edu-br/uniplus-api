namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.Text.Json;

using Confluent.Kafka;

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
/// chave da mensagem resume as chaves dos registros; quem consome separa o lote e deduplica por <c>chave</c> de
/// registro. A deduplicação vale para a nova tentativa dentro da mesma execução; uma nova requisição do usuário é um
/// novo acesso, com registros próprios. O valor leva o registro sem o dado, só o código do fato.
/// </remarks>
public sealed class RegistroDeAcessoKafka : IRegistroOperacionalRestrito, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Func<IProducer<string, string>> _fabrica;
    private readonly Lock _trava = new();
    private readonly string _topico;
    private readonly TimeSpan _prazo;
    private IProducer<string, string> _produtor;

    /// <summary>
    /// Cria o produtor com a configuração de <c>RegistroDeAcesso:Kafka</c>. Configuração inválida, inclusive sem
    /// bootstrap servers, lança <see cref="OptionsValidationException"/> ao ler as opções: não há registrador silencioso.
    /// </summary>
    public RegistroDeAcessoKafka(IOptions<RegistroDeAcessoKafkaOptions> opcoes)
        : this(Fabrica(opcoes), opcoes.Value.Topico, opcoes.Value.PrazoDeConfirmacao)
    {
    }

    internal RegistroDeAcessoKafka(Func<IProducer<string, string>> fabrica, string topico, TimeSpan prazo)
    {
        _fabrica = fabrica;
        _topico = topico;
        _prazo = prazo;
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

        IProducer<string, string> produtor = ProdutorAtual();

        try
        {
            // A chave de cada registro é calculada uma vez e serve ao lote e ao valor da mensagem.
            EntradaDoLote[] entradas = [.. registros.Select(static r => new EntradaDoLote(r.Chave, r))];
            string chaveDoLote = RegistroDecisaoAcesso.ResumoSha256(
                string.Join('|', entradas.Select(static e => e.Chave).Order(StringComparer.Ordinal)));

            await produtor.ProduceAsync(
                _topico,
                new Message<string, string> { Key = chaveDoLote, Value = ValorDoLote(entradas) },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            // Após erro fatal o produtor idempotente não volta a funcionar: troca-o para as próximas requisições. A
            // corrente responde 503 do mesmo jeito.
            if (EhFatal(falha))
            {
                Recriar(produtor);
            }

            throw new RegistroDeAcessoNaoGravadoException(falha);
        }
    }

    /// <summary>O valor da mensagem de um lote: o que o consumidor separa, registro a registro.</summary>
    internal static string ValorDoLote(IReadOnlyList<RegistroDecisaoAcesso> registros) =>
        ValorDoLote([.. registros.Select(static r => new EntradaDoLote(r.Chave, r))]);

    private static string ValorDoLote(EntradaDoLote[] entradas) => JsonSerializer.Serialize(entradas, Json);

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

    private static Func<IProducer<string, string>> Fabrica(IOptions<RegistroDeAcessoKafkaOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        // Ler as opções valida a configuração e lança OptionsValidationException se for inválida.
        RegistroDeAcessoKafkaOptions configuracao = opcoes.Value;
        return () => new ProducerBuilder<string, string>(configuracao.ParaProdutor()).Build();
    }

    private static bool EhFatal(Exception falha) =>
        falha is KafkaException { Error.IsFatal: true } || falha.InnerException is KafkaException { Error.IsFatal: true };

    private IProducer<string, string> ProdutorAtual()
    {
        lock (_trava)
        {
            return _produtor;
        }
    }

    // Troca o produtor sob a trava e descarta o antigo em segundo plano: as chamadas em voo terminam (com erro, e então
    // 503) e a requisição corrente não espera o descarte. Falha ao criar o novo produtor também é falha de registro.
    private void Recriar(IProducer<string, string> falho)
    {
        lock (_trava)
        {
            // Outra requisição pode já ter trocado o produtor que falhou.
            if (!ReferenceEquals(_produtor, falho))
            {
                return;
            }

            try
            {
                _produtor = _fabrica();
            }
            catch (Exception falha) when (falha is not OperationCanceledException)
            {
                throw new RegistroDeAcessoNaoGravadoException(falha);
            }
        }

        _ = Task.Run(() => Descartar(falho));
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
