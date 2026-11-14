namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine.Tracking;

/// <summary>
/// Uma operação que grava em dois módulos é uma transação só (ADR-0170): a entidade da Seleção, a de Publicações e o
/// envelope do evento no outbox entram juntos, ou nenhum entra. O handler de teste roda no bus real do Host, com a unidade
/// de trabalho localizada por serviço, e grava pelos repositórios públicos de cada módulo.
/// </summary>
/// <remarks>
/// A mutação é real nos dois esquemas: o handler grava e a consulta, numa conexão à parte, só enxerga o que foi
/// confirmado. A exceção depois das gravações e a recusa que descarta as alterações não deixam linha nem envelope. O
/// controle mostra por que o descarte é do chamador: <c>Result.Failure</c> sem descartar não desfaz, e as gravações
/// confirmam. No sucesso, as duas linhas têm a mesma <c>xmin</c>, a transação que as gravou; o envelope sai do outbox depois
/// do commit, para a fila durável do evento, e o que o liga à transação é não existir nos casos em que ela não confirma.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AtomicidadeEntreDoisModulosNoHostTests : IClassFixture<HostComHandlersDeTransacao>
{
    private readonly IServiceProvider _servicos;

    public AtomicidadeEntreDoisModulosNoHostTests(MonolitoPostgresFixture fixture, HostComHandlersDeTransacao host)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(host);
        _servicos = host.Servicos(fixture);
    }

    [Fact(DisplayName = "A exceção depois das duas gravações não deixa nada em nenhum esquema nem envelope no outbox")]
    public async Task ExcecaoDepoisDasGravacoes_NaoDeixaNada()
    {
        string codigo = NovoCodigo();

        Execucao execucao = await ExecutarAsync(codigo, DesfechoDaGravacao.ExcecaoDepoisDasGravacoes);

        execucao.Excecao.Should().BeOfType<InvalidOperationException>();
        await ConferirSemNadaAsync(codigo, execucao);
    }

    [Fact(DisplayName = "A recusa de negócio que descarta as alterações não deixa nada em nenhum esquema nem envelope")]
    public async Task RecusaDescartandoAsAlteracoes_NaoDeixaNada()
    {
        string codigo = NovoCodigo();

        Execucao execucao = await ExecutarAsync(codigo, DesfechoDaGravacao.RecusaDescartandoAsAlteracoes);

        execucao.Resultado!.IsFailure.Should().BeTrue();
        await ConferirSemNadaAsync(codigo, execucao);
    }

    [Fact(DisplayName = "Controle: a recusa que não descarta as alterações não desfaz, e as duas gravações confirmam")]
    public async Task RecusaSemDescartar_AsGravacoesConfirmam()
    {
        string codigo = NovoCodigo();

        Execucao execucao = await ExecutarAsync(codigo, DesfechoDaGravacao.RecusaSemDescartar);

        execucao.Resultado!.IsFailure.Should().BeTrue();
        (await ConsultasDaTransacaoNoBanco.MotivosAsync(_servicos, codigo)).Should().Be(1);
        (await ConsultasDaTransacaoNoBanco.TiposAsync(_servicos, codigo)).Should().Be(1);
    }

    [Fact(DisplayName = "O sucesso grava os dois esquemas na mesma transação e o envelope do evento chega à fila")]
    public async Task Sucesso_GravaOsDoisEsquemasEOEnvelope()
    {
        string codigo = NovoCodigo();

        Execucao execucao = await ExecutarAsync(codigo, DesfechoDaGravacao.Sucesso);

        execucao.Resultado!.IsSuccess.Should().BeTrue();
        (await ConsultasDaTransacaoNoBanco.MotivosAsync(_servicos, codigo)).Should().Be(1);
        (await ConsultasDaTransacaoNoBanco.TiposAsync(_servicos, codigo)).Should().Be(1);
        (await ConsultasDaTransacaoNoBanco.TransacaoDoMotivoAsync(_servicos, codigo))
            .Should().Be(await ConsultasDaTransacaoNoBanco.TransacaoDoTipoAsync(_servicos, codigo), "as duas linhas foram gravadas pela mesma transação");
        execucao.Sessao!.Sent.MessagesOf<GravacaoEmDoisModulosConcluida>().Should().ContainSingle(e => e.Codigo == codigo);
        await PedidosDeConcessaoTestBase.AguardarAsync(
            async () => await ConsultasDaTransacaoNoBanco.EnvelopesDoEventoAsync(_servicos, codigo) == 1,
            "o envelope do evento não chegou à fila durável");
    }

    private static string NovoCodigo() => CodigoDeTesteDaTransacao.Novo("ATOMICIDADE");

    private async Task ConferirSemNadaAsync(string codigo, Execucao execucao)
    {
        execucao.Sessao!.Sent.MessagesOf<GravacaoEmDoisModulosConcluida>().Should().BeEmpty("o Wolverine não enviou o evento");
        (await ConsultasDaTransacaoNoBanco.MotivosAsync(_servicos, codigo)).Should().Be(0, "a Seleção não confirma sem Publicações");
        (await ConsultasDaTransacaoNoBanco.TiposAsync(_servicos, codigo)).Should().Be(0, "Publicações não confirma sem a Seleção");
        (await ConsultasDaTransacaoNoBanco.EnvelopesDoEventoAsync(_servicos, codigo)).Should().Be(0, "o evento só sai com a transação confirmada");
    }

    /// <summary>Executa o comando pelo bus e espera o Wolverine terminar tudo o que ele disparou, o envio do evento inclusive.</summary>
    private async Task<Execucao> ExecutarAsync(string codigo, DesfechoDaGravacao desfecho)
    {
        Execucao execucao = new();
        execucao.Sessao = await _servicos.GetRequiredService<IHost>()
            .TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(_ => execucao.ExecutarAsync(_servicos, new GravarEmDoisModulos(codigo, desfecho)));

        return execucao;
    }

    private sealed class Execucao
    {
        public ITrackedSession? Sessao { get; set; }

        public Result? Resultado { get; private set; }

        public Exception? Excecao { get; private set; }

        public async Task ExecutarAsync(IServiceProvider servicos, GravarEmDoisModulos comando)
        {
            await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
            try
            {
                Resultado = await escopo.ServiceProvider.GetRequiredService<ICommandBus>().Send(comando, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                Excecao = ex;
            }
        }
    }
}
