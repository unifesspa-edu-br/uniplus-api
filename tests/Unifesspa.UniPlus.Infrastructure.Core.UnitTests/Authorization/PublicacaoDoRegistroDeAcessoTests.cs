namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Diagnostics;
using System.Text;

using AwesomeAssertions;

using Confluent.Kafka;

using NSubstitute;

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
    private static readonly DateTimeOffset Agora = new(2026, 11, 1, 12, 30, 0, TimeSpan.Zero);

    private readonly IProducer<string, string> _produtor = Substitute.For<IProducer<string, string>>();
    private readonly List<Message<string, string>> _mensagens = [];

    public PublicacaoDoRegistroDeAcessoTests() =>
        _produtor.ProduceAsync(Arg.Any<string>(), Arg.Do<Message<string, string>>(_mensagens.Add), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeliveryResult<string, string>()));

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

        Message<string, string> mensagem = _mensagens.Should().ContainSingle().Subject;
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.ContentType).Should().Be("application/json");
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

        Message<string, string> mensagem = _mensagens.Should().ContainSingle().Subject;
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

        Cabecalho(_mensagens.Single(), CabecalhosDoRegistroDeAcesso.SensibilidadeMaxima).Should().Be("sensivel");
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

        Message<string, string> mensagem = _mensagens.Single();
        Cabecalho(mensagem, CabecalhosDoRegistroDeAcesso.Resultado).Should().Be(esperado);
        mensagem.Headers.Select(static h => Encoding.UTF8.GetString(h.GetValueBytes())).Should()
            .NotContain(static v => v.Contains("sem_concessao", StringComparison.Ordinal)
                || v.Contains("impedido_por", StringComparison.Ordinal), "o motivo da negativa não sai em cabeçalho");
    }

    [Fact(DisplayName = "Nenhum cabeçalho leva o subject, o requestId nem o id da inscrição, ainda que o requestId traga dígitos de CPF")]
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

    private RegistroDeAcessoKafka Criar() => new(() => _produtor, "topico", TimeSpan.FromSeconds(1), new RelogioFixo(Agora));

    private static string? Cabecalho(Message<string, string> mensagem, string nome) =>
        mensagem.Headers.TryGetLastBytes(nome, out byte[]? bytes) ? Encoding.UTF8.GetString(bytes) : null;

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
