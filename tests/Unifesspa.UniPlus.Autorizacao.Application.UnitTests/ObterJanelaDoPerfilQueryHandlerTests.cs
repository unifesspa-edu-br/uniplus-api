namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
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

public sealed class ObterJanelaDoPerfilQueryHandlerTests
{
    private const string ConsultarVersoes = "autorizacao:perfis:consultar-versoes";

    private static readonly Guid PerfilId = Guid.CreateVersion7();

    [Fact(DisplayName = "A leitura da janela recusa quem não pode ler o perfil sem consultá-lo e registra as negativas")]
    public async Task Obter_SemPermissao_RecusaSemConsultarERegistraAsNegativas()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        VerificadorDeTeste verificador = new(PerfisDeTeste.Autor.Sub, []);

        Result<JanelaDeMigracaoDto> resultado = await Obter(verificador, perfis);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        perfis.ReceivedCalls().Should().BeEmpty();
        TrilhaDeAcesso.Do(verificador).Should().BeEquivalentTo(
            new[] { Manter, Gestao, Protecao, ConsultarVersoes }.Select(p => new TrilhaDeAcesso(
                PerfisDeTeste.Autor.Sub, p, ResourceTiposAutorizacao.Perfil, PerfilId, ResultadoDoAcesso.Negado)));
    }

    [Fact(DisplayName = "A leitura da janela recusa o token sem autor sem consultar o perfil nem registrar")]
    public async Task Obter_SemAutor_RecusaSemConsultarNemRegistrar()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        Result<JanelaDeMigracaoDto> resultado = await Obter(verificador, perfis);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        perfis.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Fact(DisplayName = "A leitura da janela de um perfil inexistente é não encontrada")]
    public async Task Obter_PerfilInexistente_NaoEncontrado()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Gestao]);

        Result<JanelaDeMigracaoDto> resultado = await Obter(verificador, new PerfisNaMemoria());

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        TrilhaDeAcesso.Do(verificador).Should().Equal(GestaoPermitidaSobre(PerfilId));
    }

    [Fact(DisplayName = "A leitura da janela de um perfil que nunca teve janela é não encontrada")]
    public async Task Obter_PerfilSemJanela_JanelaNaoEncontrada()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["a:b:c"], new CatalogoDeTeste());

        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Gestao]);

        Result<JanelaDeMigracaoDto> resultado = await Obter(verificador, new PerfisNaMemoria(perfil), perfil.Id);

        resultado.Error!.Code.Should().Be(MigracaoErrorCodes.JanelaNaoEncontrada);
        TrilhaDeAcesso.Do(verificador).Should().Equal(GestaoPermitidaSobre(perfil.Id));
    }

    [Fact(DisplayName = "A leitura da janela de um perfil que tem janela devolve a janela e registra a decisão no perfil")]
    public async Task Obter_PerfilComJanela_DevolveAJanelaERegistra()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["a:b:c"], new CatalogoDeTeste());
        JanelaDeMigracao janela = JanelaDeMigracao.Abrir(perfil.Id, perfil.Versoes[^1], DateTimeOffset.UtcNow);
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Gestao]);

        Result<JanelaDeMigracaoDto> resultado = await Obter(verificador, new PerfisNaMemoria(perfil), perfil.Id, new JanelasNaMemoria(ultima: janela));

        resultado.Value!.Should().Match<JanelaDeMigracaoDto>(d => d.Id == janela.Id && d.PerfilId == perfil.Id && d.Prazo == janela.Prazo && d.Situacao == "encerrando");
        TrilhaDeAcesso.Do(verificador).Should().Equal(GestaoPermitidaSobre(perfil.Id));
    }

    private static TrilhaDeAcesso GestaoPermitidaSobre(Guid perfilId) =>
        new(PerfisDeTeste.Gestor.Sub, Gestao, ResourceTiposAutorizacao.Perfil, perfilId, ResultadoDoAcesso.Permitido);

    private static Task<Result<JanelaDeMigracaoDto>> Obter(IVerificadorDeAcesso verificador, IPerfilRepository perfis, Guid? perfilId = null, JanelasNaMemoria? janelasNaMemoria = null)
    {
        JanelasNaMemoria janelas = janelasNaMemoria ?? new();
        LeitorDaMigracao leitor = new(janelas, Substitute.For<ICatalogoDePermissoesConsultavel>(), TimeProvider.System);

        return ObterJanelaDoPerfilQueryHandler.Handle(
            new ObterJanelaDoPerfilQuery(perfilId ?? PerfilId), verificador, perfis,
            new LeituraDaJanela(janelas, leitor, TimeProvider.System), new Usuario(PerfisDeTeste.Gestor), CancellationToken.None);
    }
}
