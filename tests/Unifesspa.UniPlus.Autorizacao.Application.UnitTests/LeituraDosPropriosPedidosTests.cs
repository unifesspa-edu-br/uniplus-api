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
/// Toda leitura que a parte faz dos próprios pedidos entra na trilha, um registro por pedido exibido na fila e um por
/// leitura do pedido: pela consulta dos pedidos, quando a parte a tem (UNI-REQ-0372), ou por titularidade, quando não a
/// tem (ADR-0166). Cecília Moura é o Encarregado de dados e pediu os dois pedidos para si mesma; Betina das Neves não tem
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
        registros.Should().OnlyContain(static r => r.Permissao == Consultar && r.FonteGrant == "concessao" && r.ResourceTipo == ResourceTiposAutorizacao.Pedido);
    }

    [Fact(DisplayName = "Na fila dos próprios pedidos, quem não tem a consulta grava um registro por pedido exibido, por titularidade")]
    public async Task FilaMeus_SemAConsulta_UmRegistroPorPedidoPorTitularidade()
    {
        PedidoDeConcessao[] pedidos = [Pedido(Betina), Pedido(Betina)];
        VerificadorDeTeste verificador = new(Betina.Identidade.Sub, permitidas: []);

        Result<ListarPedidosDeConcessaoResult> pagina = await ListarMeusAsync(Betina, pedidos, verificador);

        pagina.Value!.Items.Should().HaveCount(2);
        IReadOnlyCollection<RegistroDecisaoAcesso> registros = verificador.Gravados.Should().ContainSingle().Subject;
        registros.Select(static r => r.ResourceId).Should().Equal(pedidos.Select(static p => (Guid?)p.Id));
        registros.Should().AllSatisfy(static r => DeveSerPorTitularidade(r, Betina));
    }

    [Theory(DisplayName = "Na fila dos próprios pedidos, a página vazia não grava nada, com ou sem a consulta")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FilaMeus_PaginaVazia_NaoGrava(bool comAConsulta)
    {
        VerificadorDeTeste verificador = new(Betina.Identidade.Sub, permitidas: comAConsulta ? [Consultar] : []);

        Result<ListarPedidosDeConcessaoResult> pagina = await ListarMeusAsync(Betina, [], verificador);

        pagina.Value!.Items.Should().BeEmpty();
        verificador.Gravados.Should().BeEmpty("sem pedido exibido não há leitura a registrar, e o registro nem é consultado");
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

    [Fact(DisplayName = "No pedido em que é parte, quem não tem a consulta vê o pedido e grava a leitura dele por titularidade")]
    public async Task Parte_SemAConsulta_VeERegistraPorTitularidade()
    {
        PedidoDeConcessao pedido = Pedido(Betina);
        VerificadorDeTeste verificador = new(Betina.Identidade.Sub, permitidas: []);

        DomainError? recusa = await VisibilidadeDoPedido.ConferirAsync(pedido, Betina, verificador, CancellationToken.None);

        recusa.Should().BeNull();
        RegistroDecisaoAcesso registro = verificador.Gravados.Should().ContainSingle().Which.Should().ContainSingle().Subject;
        registro.ResourceId.Should().Be(pedido.Id);
        DeveSerPorTitularidade(registro, Betina);
    }

    [Fact(DisplayName = "O beneficiário pelo e-mail verificado, antes de aceitar e sem a consulta, grava a leitura do pedido por titularidade")]
    public async Task BeneficiarioPeloEmail_SemAConsulta_RegistraPorTitularidade()
    {
        PedidoDeConcessao pedido = Pedido(Cecilia, Betina.Email);
        VerificadorDeTeste verificador = new(Betina.Identidade.Sub, permitidas: []);

        DomainError? recusa = await VisibilidadeDoPedido.ConferirAsync(pedido, Betina, verificador, CancellationToken.None);

        recusa.Should().BeNull();
        RegistroDecisaoAcesso registro = verificador.Gravados.Should().ContainSingle().Which.Should().ContainSingle().Subject;
        registro.ResourceId.Should().Be(pedido.Id);
        DeveSerPorTitularidade(registro, Betina);
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

        recusa.Should().NotBeNull("sem sujeito não se sabe por qual origem registrar a leitura, e o pedido não sai");
        recusa!.Code.Should().Be(PedidoErrorCodes.IdentidadeIncompleta);
        await NaoGravouAsync(verificador);
    }

    [Fact(DisplayName = "Na fila dos próprios pedidos, sem identidade ao gravar por titularidade, a página não sai e a recusa é a identidade incompleta")]
    public async Task FilaMeus_SemIdentidadeAoGravarPorTitularidade_IdentidadeIncompleta()
    {
        IVerificadorDeAcesso verificador = SemIdentidadeAoGravarPorTitularidade();

        Result<ListarPedidosDeConcessaoResult> pagina = await ListarMeusAsync(Betina, [Pedido(Betina)], verificador);

        (pagina.Error?.Code, pagina.Value).Should().Be((PedidoErrorCodes.IdentidadeIncompleta, (ListarPedidosDeConcessaoResult?)null));
    }

    private static void DeveSerPorTitularidade(RegistroDecisaoAcesso registro, PessoaNoToken leitor)
    {
        (registro.AtorSubject, registro.Permissao, registro.FonteGrant, registro.ResourceTipo, registro.Sensibilidade, registro.Resultado)
            .Should().Be((leitor.Identidade.Sub, AcessoDoAprovador.CodigoDaLeituraPorTitularidade, "titularidade", ResourceTiposAutorizacao.Pedido, "pessoal", ResultadoDoAcesso.Permitido));
        (registro.GrantId, registro.PerfilId, registro.VersaoId, registro.EscopoUnidadeId).Should().Be(((Guid?)null, (Guid?)null, (Guid?)null, (Guid?)null));
    }

    // O token identifica a pessoa (emissor e sub), mas a decisão não tem sujeito, como no token sem jti.
    private static IVerificadorDeAcesso SemSujeitoNaDecisao()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        return verificador;
    }

    // A consulta dos pedidos é negada, e o token deixa de identificar a pessoa quando a leitura vai ser gravada.
    private static IVerificadorDeAcesso SemIdentidadeAoGravarPorTitularidade()
    {
        VerificadorDeTeste semAConsulta = new(Betina.Identidade.Sub, permitidas: []);
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default)
            .ReturnsForAnyArgs(chamada => semAConsulta.AvaliarAsync(chamada.ArgAt<PermissionRequirement>(0), chamada.ArgAt<ResourceContext?>(1)));
        verificador.RegistrarLeituraPorTitularidadeAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(ResultadoDoAcesso.IdentidadeIncompleta);

        return verificador;
    }

    private static async Task NaoGravouAsync(IVerificadorDeAcesso verificador)
    {
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
        await verificador.DidNotReceiveWithAnyArgs().RegistrarParaCadaAsync(default!, default!, default!, default, default);
        await verificador.DidNotReceiveWithAnyArgs().RegistrarLeituraPorTitularidadeAsync(default!, default!, default!, default);
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

    private static PedidoDeConcessao Pedido(PessoaNoToken solicitante, string? emailDoBeneficiario = null) =>
        PedidoDeConcessao.Criar(
            new PartesDoPedido(solicitante, emailDoBeneficiario),
            PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["geral:pedidos:consultar"], new CatalogoInstitucional()),
            EscopoDaConcessao.DaInstituicao(),
            new DeclaracaoDoPedido("Acompanha os pedidos de atendimento do setor."),
            new DominiosDeTeste(),
            new CatalogoInstitucional(),
            DateTimeOffset.UtcNow).Value!;
}
