namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>
/// A leitura dos próprios pedidos por quem tem a consulta dos pedidos entra na trilha por ela, um registro por pedido
/// exibido na fila e um por leitura do pedido (UNI-REQ-0372). Sem a consulta, a pessoa lê os próprios pedidos sem
/// registro. Cecília Moura é o Encarregado de dados e pediu os dois pedidos para si mesma; Betina das Neves não tem
/// permissão alguma e pediu os dela.
/// </summary>
public sealed class LeituraDosPropriosPedidosTests
{
    private const string Consultar = "autorizacao:pedidos:consultar";

    private static readonly PessoaNoToken Cecilia = new(new Identidade(Emissor, "cecilia.moura"), "Cecília Moura", "cecilia_ester_moura@uniplus-test.local", true);
    private static readonly PessoaNoToken Betina = new(new Identidade(Emissor, "betina.neves"), "Betina das Neves", "betina-dasneves81@uniplus-test.local", true);

    [Fact(DisplayName = "Na fila dos próprios pedidos, quem tem a consulta grava um registro por pedido exibido, pela consulta")]
    public async Task FilaMeus_ComAConsulta_UmRegistroPorPedido()
    {
        PedidoDeConcessao[] pedidos = [Pedido(Cecilia), Pedido(Cecilia)];
        VerificadorDeTeste verificador = new(Cecilia.Identidade.Sub, permitidas: [Consultar]);

        Result<ListarPedidosDeConcessaoResult> pagina = await ListarMeusAsync(Cecilia, pedidos, verificador);

        pagina.Value!.Items.Should().HaveCount(2);
        IReadOnlyCollection<RegistroDecisaoAcesso> registros = verificador.Gravados.Should().ContainSingle().Subject;
        registros.Select(static r => r.ResourceId).Should().Equal(pedidos.Select(static p => (Guid?)p.Id));
        registros.Should().OnlyContain(static r => r.Permissao == Consultar && r.Permitido && r.ResourceTipo == ResourceTiposAutorizacao.Pedido);
    }

    [Fact(DisplayName = "Na fila dos próprios pedidos, quem não tem a consulta lê a página inteira sem registro")]
    public async Task FilaMeus_SemAConsulta_PaginaSemRegistro()
    {
        VerificadorDeTeste verificador = new(Betina.Identidade.Sub, permitidas: []);

        Result<ListarPedidosDeConcessaoResult> pagina = await ListarMeusAsync(Betina, [Pedido(Betina), Pedido(Betina)], verificador);

        pagina.Value!.Items.Should().HaveCount(2);
        verificador.Gravados.Should().BeEmpty("a leitura por titularidade, sem a consulta, segue sem registro");
    }

    [Fact(DisplayName = "No pedido em que é parte, quem tem a consulta vê o pedido e grava a leitura dele pela consulta")]
    public async Task Parte_ComAConsulta_VeERegistraPelaConsulta()
    {
        PedidoDeConcessao pedido = Pedido(Cecilia);
        VerificadorDeTeste verificador = new(Cecilia.Identidade.Sub, permitidas: [Consultar]);

        DomainError? recusa = await VisibilidadeDoPedido.ConferirAsync(pedido, Cecilia, verificador, CancellationToken.None);

        recusa.Should().BeNull();
        TrilhaDeAcesso.Do(verificador).Should().Equal(
            new TrilhaDeAcesso(Cecilia.Identidade.Sub, Consultar, ResourceTiposAutorizacao.Pedido, pedido.Id, ResultadoDoAcesso.Permitido));
    }

    [Fact(DisplayName = "No pedido em que é parte, quem não tem a consulta vê o pedido sem registro")]
    public async Task Parte_SemAConsulta_VeSemRegistro()
    {
        VerificadorDeTeste verificador = new(Betina.Identidade.Sub, permitidas: []);

        DomainError? recusa = await VisibilidadeDoPedido.ConferirAsync(Pedido(Betina), Betina, verificador, CancellationToken.None);

        recusa.Should().BeNull();
        verificador.Gravados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Na fila dos próprios pedidos, a decisão sem sujeito recusa pela identidade incompleta, sem registro e sem pedido")]
    public async Task FilaMeus_SemSujeito_IdentidadeIncompletaSemRegistro()
    {
        IVerificadorDeAcesso verificador = SemSujeitoNaDecisao();

        Result<ListarPedidosDeConcessaoResult> pagina = await ListarMeusAsync(Cecilia, [Pedido(Cecilia)], verificador);

        (pagina.Error?.Code, pagina.Value).Should().Be((PedidoErrorCodes.IdentidadeIncompleta, (ListarPedidosDeConcessaoResult?)null));
        await NaoGravouAsync(verificador);
    }

    [Fact(DisplayName = "No pedido em que é parte, a decisão sem sujeito recusa pela identidade incompleta, sem registro")]
    public async Task Parte_SemSujeito_IdentidadeIncompletaSemRegistro()
    {
        IVerificadorDeAcesso verificador = SemSujeitoNaDecisao();

        DomainError? recusa = await VisibilidadeDoPedido.ConferirAsync(Pedido(Cecilia), Cecilia, verificador, CancellationToken.None);

        recusa.Should().NotBeNull("sem sujeito não se sabe se a leitura exige registro, e o pedido não sai");
        recusa!.Code.Should().Be(PedidoErrorCodes.IdentidadeIncompleta);
        await NaoGravouAsync(verificador);
    }

    // O token identifica a pessoa (emissor e sub), mas a decisão não tem sujeito, como no token sem jti.
    private static IVerificadorDeAcesso SemSujeitoNaDecisao()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        return verificador;
    }

    private static async Task NaoGravouAsync(IVerificadorDeAcesso verificador)
    {
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
        await verificador.DidNotReceiveWithAnyArgs().RegistrarParaCadaAsync(default!, default!, default!, default, default);
    }

    private static Task<Result<ListarPedidosDeConcessaoResult>> ListarMeusAsync(
        PessoaNoToken pessoa, PedidoDeConcessao[] pedidos, IVerificadorDeAcesso verificador)
    {
        IPedidoDeConcessaoRepository repository = Substitute.For<IPedidoDeConcessaoRepository>();
        repository.ListarPaginadoAsync(default!, default, default, default, default, default)
            .ReturnsForAnyArgs(([.. pedidos.Select(static p => new PedidoComPerfil(p, "Análise de atendimento", 1))], null, null));

        return ListarPedidosDeConcessaoQueryHandler.Handle(
            new ListarPedidosDeConcessaoQuery(FilaDePedidos.Meus, null, null, null, 10, PaginationDirection.Next),
            repository, verificador, new Usuario(pessoa.Identidade), CancellationToken.None);
    }

    private static PedidoDeConcessao Pedido(PessoaNoToken solicitante) =>
        PedidoDeConcessao.Criar(
            new PartesDoPedido(solicitante, null),
            PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["geral:pedidos:consultar"], new CatalogoInstitucional()),
            EscopoDaConcessao.DaInstituicao(),
            new DeclaracaoDoPedido("Acompanha os pedidos de atendimento do setor."),
            new DominiosDeTeste(),
            new CatalogoInstitucional(),
            DateTimeOffset.UtcNow).Value!;
}
