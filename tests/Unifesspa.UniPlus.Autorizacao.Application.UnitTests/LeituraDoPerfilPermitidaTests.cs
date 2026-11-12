namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class LeituraDoPerfilPermitidaTests
{
    private static readonly CatalogoDaAplicacao Catalogo = new([]);

    [Fact(DisplayName = "A listagem do token sem autor é recusada sem consultar os perfis nem registrar")]
    public async Task Listar_SemAutor_RecusaSemConsultarNemRegistrar()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IVerificadorDeAcesso verificador = SemAutor();

        Result<ListarPerfisResult> resultado = await Listar(verificador, perfis);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        perfis.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Fact(DisplayName = "A listagem permitida à gestão de acessos devolve os perfis e registra a decisão na operação")]
    public async Task Listar_PelaGestao_ListaERegistra()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Gestao]);

        Result<ListarPerfisResult> resultado = await Listar(verificador, PerfisVazios());

        resultado.IsSuccess.Should().BeTrue();
        TrilhaDeAcesso.Do(verificador).Should().Equal(Trilha(Gestao, ResourceTipos.Operacao, null));
    }

    [Fact(DisplayName = "A leitura de um perfil do token sem autor é recusada sem consultar o perfil nem registrar")]
    public async Task Obter_SemAutor_RecusaSemConsultarNemRegistrar()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IVerificadorDeAcesso verificador = SemAutor();

        Result<PerfilDto> resultado = await Obter(Guid.CreateVersion7(), verificador, perfis);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        perfis.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Fact(DisplayName = "A leitura permitida à gestão de acessos devolve o perfil e registra a decisão no perfil")]
    public async Task Obter_PelaGestao_DevolveORegistra()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["a:b:c"], new CatalogoDeTeste());
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Gestao]);

        Result<PerfilDto> resultado = await Obter(perfil.Id, verificador, new PerfisNaMemoria(perfil));

        resultado.Value!.Id.Should().Be(perfil.Id);
        TrilhaDeAcesso.Do(verificador).Should().Equal(Trilha(Gestao, ResourceTiposAutorizacao.Perfil, perfil.Id));
    }

    [Fact(DisplayName = "A leitura permitida de um perfil inexistente é não encontrada e registra a decisão no perfil consultado")]
    public async Task Obter_PerfilInexistente_NaoEncontradoERegistra()
    {
        Guid perfilId = Guid.CreateVersion7();
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Gestao]);

        Result<PerfilDto> resultado = await Obter(perfilId, verificador, new PerfisNaMemoria());

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        TrilhaDeAcesso.Do(verificador).Should().Equal(Trilha(Gestao, ResourceTiposAutorizacao.Perfil, perfilId));
    }

    private static TrilhaDeAcesso Trilha(string permissao, string resourceTipo, Guid? resourceId) =>
        new(PerfisDeTeste.Gestor.Sub, permissao, resourceTipo, resourceId, ResultadoDoAcesso.Permitido);

    private static IVerificadorDeAcesso SemAutor()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));
        return verificador;
    }

    private static IPerfilRepository PerfisVazios()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        perfis.ListarPaginadoAsync(default, default, default, default).ReturnsForAnyArgs(([], null, null));
        return perfis;
    }

    private static Task<Result<ListarPerfisResult>> Listar(IVerificadorDeAcesso verificador, IPerfilRepository perfis) =>
        ListarPerfisQueryHandler.Handle(new ListarPerfisQuery(null, 10, default), verificador, perfis, Catalogo, CancellationToken.None);

    private static Task<Result<PerfilDto>> Obter(Guid perfilId, IVerificadorDeAcesso verificador, IPerfilRepository perfis)
    {
        IJanelaDeMigracaoRepository janelas = Substitute.For<IJanelaDeMigracaoRepository>();
        LeitorDaMigracao leitor = new(janelas, Substitute.For<ICatalogoDePermissoesConsultavel>(), TimeProvider.System);
        DetalheDoPerfil detalhe = new(perfis, Catalogo, new LeituraDaJanela(janelas, leitor, TimeProvider.System), Substitute.For<IUserContext>());

        return ObterPerfilQueryHandler.Handle(new ObterPerfilQuery(perfilId), verificador, detalhe, CancellationToken.None);
    }
}
