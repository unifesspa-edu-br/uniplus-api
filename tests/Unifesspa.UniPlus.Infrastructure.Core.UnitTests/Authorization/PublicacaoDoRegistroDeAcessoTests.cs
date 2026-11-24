namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Diagnostics;
using System.Text;

using AwesomeAssertions;

using Confluent.Kafka;

using NSubstitute;

using unifesspa.uniplus.autorizacao.registrodeacesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization.RegistroDeAcessoDeTeste;

/// <summary>
/// A mensagem do lote leva a execução como chave, para que os lotes de uma requisição caiam na mesma partição, e os
/// cabeçalhos que o consumidor usa para processar o valor sem abri-lo.
/// </summary>
public sealed class PublicacaoDoRegistroDeAcessoTests
{
    // Margem do teste, bem acima do prazo curto dos cenários: serve só para não deixar o teste pendurado se o prazo falhar.
    private static readonly TimeSpan LimiteDoTeste = TimeSpan.FromSeconds(10);

    private static readonly DateTimeOffset Agora = new(2026, 11, 1, 12, 30, 0, TimeSpan.Zero);

    private readonly IProducer<string, byte[]> _produtor = Substitute.For<IProducer<string, byte[]>>();
    private readonly List<Message<string, byte[]>> _mensagens = [];

    public PublicacaoDoRegistroDeAcessoTests() =>
        _produtor.ProduceAsync(Arg.Any<string>(), Arg.Do<Message<string, byte[]>>(_mensagens.Add), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));

    [Fact(DisplayName = "A chave da mensagem é a execução, e dois lotes da mesma requisição saem com a mesma chave")]
    public async Task Chave_EAExecucao()
    {
        using RegistroDeAcessoKafka registro = Criar();

        await registro.RegistrarAsync([Registro(resourceId: Guid.CreateVersion7())], default);
        await registro.RegistrarAsync([Registro(resultado: ResultadoDoAcesso.Negado)], default);

        _mensagens.Select(static m => m.Key).Should().Equal(Execucao.ToString(), Execucao.ToString());
        Cabecalho(_mensagens[0], CabecalhosDoRegistroDeAcesso.ExecucaoId).Should().Be(Execucao.ToString());
    }

    [Fact(DisplayName = "Lote com registros de execuções diferentes lança erro antes de produzir")]
    public async Task LoteInconsistente_LancaAntesDeProduzir()
    {
        using RegistroDeAcessoKafka registro = Criar();

        Func<Task> acao = () => registro.RegistrarAsync([Registro(), Registro(execucaoId: Guid.CreateVersion7())], default);

        await acao.Should().ThrowAsync<InvalidOperationException>();
        await _produtor.DidNotReceiveWithAnyArgs().ProduceAsync(default(string)!, default!, default);
    }

    [Fact(DisplayName = "Os cabeçalhos de identificação do lote levam os valores esperados")]
    public async Task Cabecalhos_ComOsValoresEsperados()
    {
        using Activity atividade = new Activity("requisicao").Start();
        using RegistroDeAcessoKafka registro = Criar();

        await registro.RegistrarAsync(
            [Registro(clienteOidc: "selecao-web"), Registro(clienteOidc: "selecao-web", resourceId: Guid.CreateVersion7())], default);

        Message<string, byte[]> mensagem = _mensagens.Should().ContainSingle().Subject;
        mensagem.Headers.TryGetLastBytes("content-type", out _).Should().BeFalse("o valor é Avro, com o schema-id no envelope");
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.Tipo).Should().Be("registro-de-acesso.lote");
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.VersaoSchema).Should().Be("1");
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.ProduzidoEm).Should().Be("2026-11-01T12:30:00.0000000Z");
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.ClienteOidc).Should().Be("selecao-web");
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.Quantidade).Should().Be("2");
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.Traceparent).Should().Be(atividade.Id);
    }

    [Fact(DisplayName = "Sem client OIDC e sem atividade corrente, os cabeçalhos cliente-oidc e traceparent são omitidos")]
    public async Task CabecalhosOpcionais_OmitidosQuandoNulos()
    {
        Activity.Current = null;
        using RegistroDeAcessoKafka registro = Criar();

        await registro.RegistrarAsync([Registro()], default);

        Message<string, byte[]> mensagem = _mensagens.Should().ContainSingle().Subject;
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.ClienteOidc).Should().BeNull();
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.Traceparent).Should().BeNull();
    }

    [Fact(DisplayName = "A sensibilidade máxima é a maior do lote")]
    public async Task SensibilidadeMaxima_EAMaiorDoLote()
    {
        using RegistroDeAcessoKafka registro = Criar();

        await registro.RegistrarAsync(
            [
                Registro(sensibilidade: ClassificacaoDeProtecao.Interno),
                Registro(sensibilidade: ClassificacaoDeProtecao.Sensivel),
                Registro(sensibilidade: ClassificacaoDeProtecao.Pessoal),
            ],
            default);

        Cabecalho(_mensagens.Single(), CabecalhosDoRegistroDeAcesso.SensibilidadeMaxima).Should().Be("SENSIVEL");
    }

    [Theory(DisplayName = "O resultado do lote é o de todos os registros, ou misto quando divergem")]
    [InlineData(new[] { ResultadoDoAcesso.Permitido, ResultadoDoAcesso.Permitido }, "permitido")]
    [InlineData(new[] { ResultadoDoAcesso.Negado, ResultadoDoAcesso.Negado }, "negado")]
    [InlineData(new[] { ResultadoDoAcesso.Impedido }, "impedido")]
    [InlineData(new[] { ResultadoDoAcesso.Permitido, ResultadoDoAcesso.Negado }, "misto")]
    [InlineData(new[] { ResultadoDoAcesso.Negado, ResultadoDoAcesso.Impedido }, "misto")]
    public async Task Resultado_DerivaDosRegistros(ResultadoDoAcesso[] resultados, string esperado)
    {
        using RegistroDeAcessoKafka registro = Criar();

        await registro.RegistrarAsync([.. resultados.Select(r => Registro(resultado: r))], default);

        Message<string, byte[]> mensagem = _mensagens.Single();
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.Resultado).Should().Be(esperado);
        mensagem.Headers.Select(static h => Encoding.UTF8.GetString(h.GetValueBytes())).Should()
            .NotContain(static v => v.Contains("sem_concessao", StringComparison.Ordinal)
                || v.Contains("impedido_por", StringComparison.Ordinal), "o motivo da negativa não sai em cabeçalho");
    }

    [Fact(DisplayName = "Nenhum cabeçalho leva o identificador da conta, o requestId nem o id da inscrição, ainda que o requestId traga dígitos de CPF")]
    public async Task Cabecalhos_NaoVazamDadoDoAtorNemDoResource()
    {
        Guid inscricao = Guid.CreateVersion7();
        using RegistroDeAcessoKafka registro = Criar();

        await registro.RegistrarAsync([Registro(requestId: "52998224725", resourceId: inscricao)], default);

        string[] valores = [.. _mensagens.Single().Headers.Select(static h => Encoding.UTF8.GetString(h.GetValueBytes()))];
        valores.Should().NotContain(v => v.Contains("leandro.viana", StringComparison.Ordinal)
            || v.Contains("52998224725", StringComparison.Ordinal)
            || v.Contains(inscricao.ToString(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O valor da mensagem é o lote serializado pelo serializador do esquema")]
    public async Task Valor_EOLoteSerializado()
    {
        using RegistroDeAcessoKafka registro = Criar();

        await registro.RegistrarAsync([Registro()], default);

        _mensagens.Single().Value.Should().Equal(SerializadorDoLoteDeTeste.BytesDoLote);
    }

    [Fact(DisplayName = "Falha ao serializar, com o Schema Registry fora, é falha de gravação e nada chega ao produtor")]
    public async Task FalhaDeSerializacao_NaoChegaAoProdutor()
    {
        using RegistroDeAcessoKafka registro = Criar(new SerializadorDoLoteDeTeste(static () => throw new HttpRequestException("fora do ar")));

        Func<Task> acao = () => registro.RegistrarAsync([Registro()], default);

        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
        _mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Serializador que não responde no prazo da confirmação é falha de gravação, sem esperar o Schema Registry")]
    public async Task SerializadorQueNaoResponde_FalhaNoPrazoDaConfirmacao()
    {
        TaskCompletionSource<byte[]> nuncaTermina = new();
        using RegistroDeAcessoKafka registro = CriarComPrazoCurto(() => nuncaTermina.Task);

        Func<Task> acao = () => registro.RegistrarAsync([Registro()], default).WaitAsync(LimiteDoTeste);

        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
        _mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Requisição cancelada durante a serialização para sem produzir o lote")]
    public async Task RequisicaoCancelada_NaoProduzOLote()
    {
        TaskCompletionSource<byte[]> parado = new();
        using RegistroDeAcessoKafka registro = CriarComPrazoCurto(() => parado.Task, TimeSpan.FromMinutes(1));
        using CancellationTokenSource cancelamento = new();

        Task chamada = registro.RegistrarAsync([Registro()], cancelamento.Token);
        await cancelamento.CancelAsync();
        parado.SetResult(SerializadorDoLoteDeTeste.BytesDoLote);

        await chamada.Invoking(static c => c.WaitAsync(LimiteDoTeste)).Should().ThrowAsync<OperationCanceledException>();
        _mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o serializador parado, chamadas concorrentes falham no prazo, só uma serialização fica em voo e o registro se recupera")]
    public async Task SerializadorParado_UmaSerializacaoEmVoo_ERecuperaQuandoVolta()
    {
        TaskCompletionSource<byte[]> parado = new();
        int invocacoes = 0;
        using RegistroDeAcessoKafka registro = CriarComPrazoCurto(() => ++invocacoes == 1 ? parado.Task : Task.FromResult(SerializadorDoLoteDeTeste.BytesDoLote));

        await TodasFalhamNoPrazoAsync(registro);
        invocacoes.Should().Be(1);
        _mensagens.Should().BeEmpty();

        parado.SetResult(SerializadorDoLoteDeTeste.BytesDoLote);
        await registro.RegistrarAsync([Registro()], default);

        _mensagens.Should().ContainSingle();
    }

    private static async Task TodasFalhamNoPrazoAsync(RegistroDeAcessoKafka registro)
    {
        Task[] chamadas = [.. Enumerable.Range(0, 3).Select(_ => registro.RegistrarAsync([Registro()], default))];
        Func<Task> todas = () => Task.WhenAll(chamadas).WaitAsync(LimiteDoTeste);

        await todas.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
        chamadas.Should().OnlyContain(static c => c.IsFaulted);
    }

    private RegistroDeAcessoKafka CriarComPrazoCurto(Func<Task<byte[]>> serializacao, TimeSpan? prazo = null) =>
        new(() => _produtor, new SerializadorDoLoteDeTeste(serializacao), "topico", prazo ?? TimeSpan.FromMilliseconds(200), new RelogioFixo(Agora));

    private RegistroDeAcessoKafka Criar(SerializadorDoLoteDeTeste? serializador = null) => new(() => _produtor, serializador ?? new SerializadorDoLoteDeTeste(), "topico", TimeSpan.FromSeconds(1), new RelogioFixo(Agora));

    private static string? Cabecalho(Message<string, byte[]> mensagem, string nome) =>
        mensagem.Headers.TryGetLastBytes(nome, out byte[]? bytes) ? Encoding.UTF8.GetString(bytes) : null;

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
