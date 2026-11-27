namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

public sealed class OfertaDePerfisTests
{
    private static readonly ListarPerfisParaPedirQuery Consulta = new(null, null, 20, PaginationDirection.Next);

    private static readonly PessoaNoToken Institucional = new(Executor, "Betina", "betina@uniplus-test.local", true);

    private static readonly PessoaNoToken Externa = new(Executor, "Paulo", "paulo@externo.test", true);

    [Fact(DisplayName = "Quem tem e-mail institucional verificado vê a oferta sem consultar a permissão")]
    public async Task Listar_Institucional_NaoConsultaPermissao()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        (string SortKey, Guid Id) proximo = ("chave", Guid.CreateVersion7());

        Result<ListarPerfisParaPedirResult> resultado = await Oferta(proximo).ListarAsync(Consulta, Institucional, verificador, CancellationToken.None);

        resultado.Value!.Items.Should().ContainSingle().Which.Nome.Should().Be("Análise de atendimento");
        resultado.Value.Proximo.Should().Be(proximo);
        await verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "O externo com a permissão de manter perfis vê a oferta")]
    public async Task Listar_ExternoComPermissao_VeAOferta()
    {
        Result<ListarPerfisParaPedirResult> resultado = await Oferta()
            .ListarAsync(Consulta, Externa, Verificador(ResultadoDoAcesso.Permitido), CancellationToken.None);

        resultado.Value!.Items.Should().ContainSingle();
    }

    [Fact(DisplayName = "O externo sem a permissão é recusado e não vê a lista")]
    public async Task Listar_ExternoSemPermissao_Recusa()
    {
        Result<ListarPerfisParaPedirResult> resultado = await Oferta()
            .ListarAsync(Consulta, Externa, Verificador(ResultadoDoAcesso.Negado), CancellationToken.None);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.SolicitanteNaoInstitucional);
    }

    [Fact(DisplayName = "O externo com identidade incompleta recebe a recusa de identidade")]
    public async Task Listar_ExternoIdentidadeIncompleta_Recusa()
    {
        Result<ListarPerfisParaPedirResult> resultado = await Oferta()
            .ListarAsync(Consulta, Externa, Verificador(ResultadoDoAcesso.IdentidadeIncompleta), CancellationToken.None);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.IdentidadeIncompleta);
    }

    [Fact(DisplayName = "Sem identidade completa no token, o handler recusa sem ler o repositório nem consultar a permissão")]
    public async Task Handle_SemIdentidade_IdentidadeIncompleta()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();

        Result<ListarPerfisParaPedirResult> resultado = await ListarPerfisParaPedirQueryHandler.Handle(
            Consulta, Oferta(perfis, null, Comum()), verificador, new DoublesDePerfis.Usuario(null), CancellationToken.None);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.IdentidadeIncompleta);
        await perfis.DidNotReceiveWithAnyArgs().ListarPediveisPaginadoAsync(default, default, default, default, default);
        await verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "O perfil que mistura unidade e instituição não é oferecido, e a paginação pedida chega ao repositório")]
    public async Task Listar_PerfilMisto_NaoEOferecido()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        Guid pontoDeContinuacao = Guid.CreateVersion7();

        Result<ListarPerfisParaPedirResult> resultado = await Oferta(perfis, null, MisturaUnidadeEInstituicao(), Comum())
            .ListarAsync(new ListarPerfisParaPedirQuery("chave", pontoDeContinuacao, 5, PaginationDirection.Prev), Institucional, Substitute.For<IVerificadorDeAcesso>(), CancellationToken.None);

        resultado.Value!.Items.Should().ContainSingle().Which.Nome.Should().Be("Análise de atendimento");
        await perfis.Received(1).ListarPediveisPaginadoAsync("chave", pontoDeContinuacao, 5, PaginationDirection.Prev, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O perfil comum da instituição sai com o escopo em código e rótulo e sem governança")]
    public async Task Listar_PerfilComum_EscopoComCodigoERotuloSemGovernanca()
    {
        Result<ListarPerfisParaPedirResult> resultado = await Oferta().ListarAsync(Consulta, Institucional, Substitute.For<IVerificadorDeAcesso>(), CancellationToken.None);

        PerfilParaPedidoDto perfil = resultado.Value!.Items.Should().ContainSingle().Which;
        (perfil.Escopo.Codigo, perfil.Escopo.Rotulo).Should().Be(("INSTITUICAO", "Instituição"));
        perfil.Governanca.Should().BeNull();
    }

    [Fact(DisplayName = "O perfil de governança sai com a governança em código e rótulo")]
    public async Task Listar_PerfilDeGovernanca_GovernancaComCodigoERotulo()
    {
        Perfil governanca = PerfisDeTeste.DeGovernanca(
            "Gestão de acessos", "Governa os acessos.", ["geral:pedidos:consultar"], new CatalogoInstitucional());

        Result<ListarPerfisParaPedirResult> resultado = await Oferta(Substitute.For<IPerfilRepository>(), null, governanca)
            .ListarAsync(Consulta, Institucional, Substitute.For<IVerificadorDeAcesso>(), CancellationToken.None);

        CodigoComRotulo<TipoDeGovernanca> tipo = resultado.Value!.Items.Should().ContainSingle().Which.Governanca!;
        (tipo.Codigo, tipo.Rotulo).Should().Be(("GESTOR_DE_ACESSOS", "Gestor de acessos"));
    }

    private static IVerificadorDeAcesso Verificador(ResultadoDoAcesso acesso)
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(acesso);

        return verificador;
    }

    private static OfertaDePerfis Oferta((string SortKey, Guid Id)? proximo = null) =>
        Oferta(Substitute.For<IPerfilRepository>(), proximo, Comum());

    private static OfertaDePerfis Oferta(IPerfilRepository perfis, (string SortKey, Guid Id)? proximo, params Perfil[] pediveis)
    {
        perfis.ListarPediveisPaginadoAsync(default, default, default, default, default)
            .ReturnsForAnyArgs((pediveis, ((string SortKey, Guid Id)?)null, proximo));

        return new OfertaDePerfis(perfis, new DominiosDeTeste(), new CatalogoComUnidade(), new CatalogoDaAplicacao([]));
    }

    private static Perfil Comum() =>
        PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["geral:pedidos:consultar"], new CatalogoInstitucional());

    // O perfil se cria com o catálogo que não distingue unidade; a oferta o confere com o que distingue, como um catálogo que mudou.
    private static Perfil MisturaUnidadeEInstituicao() =>
        PerfisDeTeste.Vigente("Perfil misto", "Mistura unidade e instituição.", ["geral:pedidos:consultar", "unidade:processos:consultar"], new CatalogoInstitucional());

    private sealed class CatalogoComUnidade : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("unidade:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }
}
