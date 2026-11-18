namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Reflection;

using AwesomeAssertions;

using Wolverine;
using Wolverine.Configuration;
using Wolverine.Transports;

/// <summary>
/// A pausa dos agentes de fila não pode deixar nenhum agente parado, em nenhum desfecho: nem se a pausa de um deles
/// falhar no meio, nem se a limpeza do teste falhar depois.
/// </summary>
public sealed class PausaDosAgentesTests
{
    [Fact]
    public async Task Pausa_e_retoma_todos_os_agentes()
    {
        AgenteQueRegistra primeiro = AgenteQueRegistra.Novo();
        AgenteQueRegistra segundo = AgenteQueRegistra.Novo();

        await using (await PausaDosAgentes.PausarAsync([primeiro.Agente, segundo.Agente]))
        {
            primeiro.Chamadas.Should().Equal("PauseAsync");
            segundo.Chamadas.Should().Equal("PauseAsync");
        }

        primeiro.Chamadas.Should().Equal("PauseAsync", "StartAsync");
        segundo.Chamadas.Should().Equal("PauseAsync", "StartAsync");
    }

    [Fact]
    public async Task Falha_ao_pausar_o_segundo_retoma_o_primeiro_e_propaga()
    {
        AgenteQueRegistra primeiro = AgenteQueRegistra.Novo();
        AgenteQueRegistra segundo = AgenteQueRegistra.Novo(falhaAoPausar: true);

        Func<Task> pausar = () => PausaDosAgentes.PausarAsync([primeiro.Agente, segundo.Agente]);

        await pausar.Should().ThrowAsync<InvalidOperationException>().WithMessage("a pausa falhou");
        primeiro.Chamadas.Should().Equal("PauseAsync", "StartAsync");
        segundo.Chamadas.Should().Equal("PauseAsync", "StartAsync");
    }

    [Fact]
    public async Task Limpeza_que_falha_dentro_da_pausa_nao_impede_a_retomada()
    {
        AgenteQueRegistra agente = AgenteQueRegistra.Novo();

        Func<Task> comLimpezaQueFalha = async () =>
        {
            await using PausaDosAgentes pausa = await PausaDosAgentes.PausarAsync([agente.Agente]);
            throw new InvalidOperationException("a limpeza da fila falhou");
        };

        await comLimpezaQueFalha.Should().ThrowAsync<InvalidOperationException>().WithMessage("a limpeza da fila falhou");
        agente.Chamadas.Should().Equal("PauseAsync", "StartAsync");
    }

    [Fact]
    public async Task Falha_ao_retomar_um_agente_nao_impede_a_retomada_dos_demais_e_todas_as_falhas_voltam()
    {
        AgenteQueRegistra primeiro = AgenteQueRegistra.Novo(falhaAoRetomar: true);
        AgenteQueRegistra segundo = AgenteQueRegistra.Novo();
        AgenteQueRegistra terceiro = AgenteQueRegistra.Novo(falhaAoRetomar: true);
        PausaDosAgentes pausa = await PausaDosAgentes.PausarAsync([primeiro.Agente, segundo.Agente, terceiro.Agente]);

        Func<Task> retomar = async () => await pausa.DisposeAsync();

        AggregateException falhas = (await retomar.Should().ThrowAsync<AggregateException>()).Which;
        falhas.InnerExceptions.Should().HaveCount(2);
        segundo.Chamadas.Should().Equal("PauseAsync", "StartAsync");
        terceiro.Chamadas.Should().Equal("PauseAsync", "StartAsync");
    }

    [Fact]
    public async Task FalhasNaPausaENaRetomada_PreservamAmbasERetomamOsDemais()
    {
        AgenteQueRegistra primeiro = AgenteQueRegistra.Novo(falhaAoRetomar: true);
        AgenteQueRegistra segundo = AgenteQueRegistra.Novo(falhaAoPausar: true);

        Func<Task> pausar = () => PausaDosAgentes.PausarAsync([primeiro.Agente, segundo.Agente]);

        AggregateException falhas = (await pausar.Should().ThrowAsync<AggregateException>()).Which.Flatten();
        falhas.InnerExceptions.Select(e => e.Message).Should().BeEquivalentTo("a pausa falhou", "a retomada falhou");
        primeiro.Chamadas.Should().Equal("PauseAsync", "StartAsync");
        segundo.Chamadas.Should().Equal("PauseAsync", "StartAsync");
    }

    [Fact]
    public async Task RetomadasCanceladas_PropagamCancelamento()
    {
        AgenteQueRegistra primeiro = AgenteQueRegistra.Novo(cancelaAoRetomar: true);
        AgenteQueRegistra segundo = AgenteQueRegistra.Novo(cancelaAoRetomar: true);
        PausaDosAgentes pausa = await PausaDosAgentes.PausarAsync([primeiro.Agente, segundo.Agente]);

        Func<Task> retomar = async () => await pausa.DisposeAsync();

        await retomar.Should().ThrowAsync<OperationCanceledException>();
        primeiro.Chamadas.Should().Equal("PauseAsync", "StartAsync");
        segundo.Chamadas.Should().Equal("PauseAsync", "StartAsync");
    }

    [Fact]
    public async Task FalhaNaPausa_RetomadasCanceladas_PreservamFalhaDaPausa()
    {
        AgenteQueRegistra primeiro = AgenteQueRegistra.Novo(cancelaAoRetomar: true);
        AgenteQueRegistra segundo = AgenteQueRegistra.Novo(falhaAoPausar: true, cancelaAoRetomar: true);

        Func<Task> pausar = () => PausaDosAgentes.PausarAsync([primeiro.Agente, segundo.Agente]);

        await pausar.Should().ThrowAsync<InvalidOperationException>().WithMessage("a pausa falhou");
        primeiro.Chamadas.Should().Equal("PauseAsync", "StartAsync");
        segundo.Chamadas.Should().Equal("PauseAsync", "StartAsync");
    }

    /// <summary>
    /// Agente de escuta que só anota as chamadas de pausa e de retomada e, se pedido, falha ao pausar. O resto da interface
    /// (circuito, fila, estado) o teste não usa.
    /// </summary>
    private sealed class AgenteQueRegistra : IListeningAgent
    {
        private readonly bool _falhaAoPausar;
        private readonly bool _falhaAoRetomar;
        private readonly bool _cancelaAoRetomar;

        private AgenteQueRegistra(bool falhaAoPausar, bool falhaAoRetomar, bool cancelaAoRetomar)
        {
            _falhaAoPausar = falhaAoPausar;
            _falhaAoRetomar = falhaAoRetomar;
            _cancelaAoRetomar = cancelaAoRetomar;
        }

        public List<string> Chamadas { get; } = [];

        public IListeningAgent Agente => this;

        public Uri Uri => new("test://agente");

        public ListeningStatus Status => ListeningStatus.Accepting;

        public Endpoint Endpoint => throw new NotSupportedException();

        public int QueueCount => 0;

        public DateTimeOffset LastQueueActivityAt => DateTimeOffset.MinValue;

        public static AgenteQueRegistra Novo(bool falhaAoPausar = false, bool falhaAoRetomar = false, bool cancelaAoRetomar = false) =>
            new(falhaAoPausar, falhaAoRetomar, cancelaAoRetomar);

        public ValueTask PauseAsync(TimeSpan pauseTime)
        {
            Chamadas.Add(nameof(PauseAsync));

            return _falhaAoPausar ? throw new InvalidOperationException("a pausa falhou") : ValueTask.CompletedTask;
        }

        public ValueTask StartAsync()
        {
            Chamadas.Add(nameof(StartAsync));
            if (_cancelaAoRetomar)
            {
                return ValueTask.FromCanceled(new CancellationToken(canceled: true));
            }

            return _falhaAoRetomar ? throw new InvalidOperationException("a retomada falhou") : ValueTask.CompletedTask;
        }

        public ValueTask StopAndDrainAsync() => throw new NotSupportedException();

        public ValueTask MarkAsTooBusyAndStopReceivingAsync() => throw new NotSupportedException();

        public Task EnqueueDirectlyAsync(IEnumerable<Envelope> envelopes) => throw new NotSupportedException();

        public ValueTask LatchPermanently() => throw new NotSupportedException();
    }
}
