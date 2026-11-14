namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

using Wolverine.Tracking;

/// <summary>
/// O registro do ato pela porta de escrita é da transação do domínio que publica (ADR-0170): a entidade da Seleção, o ato
/// e a vaga do objeto entram juntos, ou nenhum entra. O handler de teste segue o padrão do produto, no bus real do Host.
/// </summary>
/// <remarks>
/// A exceção depois da gravação não deixa linha em nenhum esquema nem envelope. A retificação de uma raiz já retificada e a
/// vaga ocupada por outro ato voltam como recusa nomeada da porta, e a Seleção não grava nada.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AtomicidadeDoRegistroDeAtoNoHostTests : IClassFixture<HostComHandlersDeTransacao>
{
    private readonly IServiceProvider _servicos;

    public AtomicidadeDoRegistroDeAtoNoHostTests(MonolitoPostgresFixture fixture, HostComHandlersDeTransacao host)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(host);
        _servicos = host.Servicos(fixture);
    }

    [Fact(DisplayName = "A exceção depois de gravar a Seleção e o ato não deixa nada em nenhum esquema nem envelope")]
    public async Task ExcecaoDepoisDeGravar_NaoDeixaNada()
    {
        RegistrarAtoPelaPorta comando = await NovoAsync(unicoPorObjeto: true, DesfechoDaGravacao.ExcecaoDepoisDasGravacoes);

        ExecucaoDeComandoNoHost execucao = await ExecucaoDeComandoNoHost.ExecutarAsync(_servicos, comando);

        execucao.Excecao.Should().BeOfType<InvalidOperationException>();
        await ConferirSemNadaAsync(comando, execucao);
    }

    [Fact(DisplayName = "O sucesso grava a Seleção, o ato e a vaga na mesma transação, e o envelope do evento chega à fila")]
    public async Task Sucesso_GravaTudoNaMesmaTransacao()
    {
        RegistrarAtoPelaPorta comando = await NovoAsync(unicoPorObjeto: true, DesfechoDaGravacao.Sucesso);

        ExecucaoDeComandoNoHost execucao = await ExecucaoDeComandoNoHost.ExecutarAsync(_servicos, comando);

        execucao.Resultado!.IsSuccess.Should().BeTrue(execucao.Resultado.Error?.Message);
        (await ConsultasDaTransacaoNoBanco.MotivosAsync(_servicos, comando.Codigo)).Should().Be(1);
        (await ConsultasDaTransacaoNoBanco.AtosAsync(_servicos, comando.AtoId)).Should().Be(1);
        (await ConsultasDaTransacaoNoBanco.VagasDoObjetoAsync(_servicos, comando.ObjetoId)).Should().Be(1);
        (await ConsultasDaTransacaoNoBanco.TransacaoDoMotivoAsync(_servicos, comando.Codigo))
            .Should().Be(await ConsultasDaTransacaoNoBanco.TransacaoDoAtoAsync(_servicos, comando.AtoId), "a Seleção e o ato foram gravados pela mesma transação");
        await PedidosDeConcessaoTestBase.AguardarAsync(
            async () => await ConsultasDaTransacaoNoBanco.EnvelopesDoEventoAsync(_servicos, comando.Codigo) == 1,
            "o envelope do evento não chegou à fila durável");
    }

    [Fact(DisplayName = "Retificar uma raiz já retificada volta como recusa nomeada e não deixa a Seleção nem o ato")]
    public async Task RaizJaRetificada_RecusaSemResiduo()
    {
        RegistrarAtoPelaPorta raiz = await NovoAsync(unicoPorObjeto: false, DesfechoDaGravacao.Sucesso);
        (await ExecucaoDeComandoNoHost.ExecutarAsync(_servicos, raiz)).Resultado!.IsSuccess.Should().BeTrue();
        RegistrarAtoPelaPorta primeira = Retificando(raiz);
        (await ExecucaoDeComandoNoHost.ExecutarAsync(_servicos, primeira)).Resultado!.IsSuccess.Should().BeTrue();
        RegistrarAtoPelaPorta segunda = Retificando(raiz);

        ExecucaoDeComandoNoHost execucao = await ExecucaoDeComandoNoHost.ExecutarAsync(_servicos, segunda);

        execucao.Resultado!.Error!.Code.Should().Be(AtoNormativoErrorCodes.RaizJaRetificada);
        await ConferirSemNadaAsync(segunda, execucao);
    }

    [Fact(DisplayName = "A vaga do objeto ocupada por outro ato volta como recusa nomeada e não deixa a Seleção nem o ato")]
    public async Task VagaOcupada_RecusaSemResiduo()
    {
        RegistrarAtoPelaPorta ocupante = await NovoAsync(unicoPorObjeto: true, DesfechoDaGravacao.Sucesso);
        (await ExecucaoDeComandoNoHost.ExecutarAsync(_servicos, ocupante)).Resultado!.IsSuccess.Should().BeTrue();
        RegistrarAtoPelaPorta intruso = ocupante with { Codigo = CodigoDeTesteDaTransacao.Novo("INTRUSO"), AtoId = Guid.CreateVersion7() };

        ExecucaoDeComandoNoHost execucao = await ExecucaoDeComandoNoHost.ExecutarAsync(_servicos, intruso);

        execucao.Resultado!.Error!.Code.Should().Be(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo);
        await ConferirSemNadaAsync(intruso, execucao);
        (await ConsultasDaTransacaoNoBanco.VagasDoObjetoAsync(_servicos, ocupante.ObjetoId)).Should().Be(1, "só a vaga do ocupante existe");
    }

    private static RegistrarAtoPelaPorta Retificando(RegistrarAtoPelaPorta raiz) =>
        raiz with { Codigo = CodigoDeTesteDaTransacao.Novo("RETIFICA"), AtoId = Guid.CreateVersion7(), AtoRetificadoId = raiz.AtoId };

    private async Task<RegistrarAtoPelaPorta> NovoAsync(bool unicoPorObjeto, DesfechoDaGravacao desfecho)
    {
        string tipo = CodigoDeTesteDaTransacao.Novo("TIPO");
        await CatalogoDeAtosDeTeste.SemearTipoAsync(_servicos, tipo, unicoPorObjeto);

        return new RegistrarAtoPelaPorta(
            CodigoDeTesteDaTransacao.Novo("REGISTRO"), Guid.CreateVersion7(), Guid.CreateVersion7(), tipo, unicoPorObjeto, desfecho);
    }

    private async Task ConferirSemNadaAsync(RegistrarAtoPelaPorta comando, ExecucaoDeComandoNoHost execucao)
    {
        execucao.Sessao!.Sent.MessagesOf<GravacaoEmDoisModulosConcluida>().Should().BeEmpty("o Wolverine não enviou o evento");
        (await ConsultasDaTransacaoNoBanco.MotivosAsync(_servicos, comando.Codigo)).Should().Be(0, "a Seleção não confirma sem o ato");
        (await ConsultasDaTransacaoNoBanco.AtosAsync(_servicos, comando.AtoId)).Should().Be(0, "o ato não confirma sem a Seleção");
        (await ConsultasDaTransacaoNoBanco.EnvelopesDoEventoAsync(_servicos, comando.Codigo)).Should().Be(0, "o evento só sai com a transação confirmada");
    }
}
