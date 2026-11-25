namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class ObterRevisaoDasConcessoesDoPerfilQueryHandlerTests
{
    private const string ConsultarVersoes = "autorizacao:perfis:consultar-versoes";

    private static readonly DateTimeOffset Agora = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PerfilId = Guid.CreateVersion7();

    [Fact(DisplayName = "A leitura da revisão recusa quem não pode ler o perfil sem consultá-lo e registra as negativas")]
    public async Task Obter_SemPermissao_RecusaSemConsultarERegistraAsNegativas()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        VerificadorDeTeste verificador = new(PerfisDeTeste.Autor.ContaId, []);

        Result<RevisaoDasConcessoesDto> resultado = await Obter(verificador, perfis);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        perfis.ReceivedCalls().Should().BeEmpty();
        TrilhaDeAcesso.Do(verificador).Should().BeEquivalentTo(
            new[] { Manter, Gestao, Protecao, ConsultarVersoes }.Select(p => new TrilhaDeAcesso(
                PerfisDeTeste.Autor.ContaId, p, ResourceTiposAutorizacao.Perfil, PerfilId, ResultadoDoAcesso.Negado)));
    }

    [Fact(DisplayName = "A leitura da revisão recusa o token sem autor sem consultar o perfil nem registrar")]
    public async Task Obter_SemAutor_RecusaSemConsultarNemRegistrar()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        Result<RevisaoDasConcessoesDto> resultado = await Obter(verificador, perfis);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        perfis.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Fact(DisplayName = "A leitura da revisão de um perfil inexistente é não encontrada")]
    public async Task Obter_PerfilInexistente_NaoEncontrado()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao]);

        Result<RevisaoDasConcessoesDto> resultado = await Obter(verificador, new PerfisNaMemoria());

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        TrilhaDeAcesso.Do(verificador).Should().Equal(GestaoPermitidaSobre(PerfilId));
    }

    [Fact(DisplayName = "A leitura da revisão de um perfil que nunca teve revisão é não encontrada")]
    public async Task Obter_PerfilSemRevisao_RevisaoNaoEncontrada()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["a:b:c"], new CatalogoDeTeste());

        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao]);

        Result<RevisaoDasConcessoesDto> resultado = await Obter(verificador, new PerfisNaMemoria(perfil), perfil.Id);

        resultado.Error!.Code.Should().Be(RevisaoDasConcessoesErrorCodes.RevisaoNaoEncontrada);
        TrilhaDeAcesso.Do(verificador).Should().Equal(GestaoPermitidaSobre(perfil.Id));
    }

    [Fact(DisplayName = "A leitura da revisão de um perfil que tem revisão devolve a revisão e registra a decisão no perfil")]
    public async Task Obter_PerfilComRevisao_DevolveARevisaoERegistra()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["a:b:c"], new CatalogoDeTeste());
        RevisaoDasConcessoesDoPerfil revisao = RevisaoDasConcessoesDoPerfil.Abrir(perfil.Id, perfil.Versoes[^1], Agora);
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao]);

        Result<RevisaoDasConcessoesDto> resultado = await Obter(verificador, new PerfisNaMemoria(perfil), perfil.Id, new RevisoesNaMemoria(ultima: revisao));

        resultado.Value!.Should().Match<RevisaoDasConcessoesDto>(d => d.Id == revisao.Id && d.PerfilId == perfil.Id && d.TerminaEm == revisao.TerminaEm && d.Situacao.Codigo == "ENCERRANDO");
        TrilhaDeAcesso.Do(verificador).Should().Equal(GestaoPermitidaSobre(perfil.Id));
    }

    private static TrilhaDeAcesso GestaoPermitidaSobre(Guid perfilId) =>
        new(PerfisDeTeste.Gestor.ContaId, Gestao, ResourceTiposAutorizacao.Perfil, perfilId, ResultadoDoAcesso.Permitido);

    private static Task<Result<RevisaoDasConcessoesDto>> Obter(IVerificadorDeAcesso verificador, IPerfilRepository perfis, Guid? perfilId = null, RevisoesNaMemoria? revisoesNaMemoria = null)
    {
        RevisoesNaMemoria revisoes = revisoesNaMemoria ?? new();
        TimeProvider relogio = Substitute.For<TimeProvider>();
        relogio.GetUtcNow().Returns(Agora);
        LeitorDaMigracao leitor = new(revisoes, Substitute.For<ICatalogoDePermissoesConsultavel>(), relogio);

        return ObterRevisaoDasConcessoesDoPerfilQueryHandler.Handle(
            new ObterRevisaoDasConcessoesDoPerfilQuery(perfilId ?? PerfilId), verificador, perfis,
            new LeituraDaRevisaoDasConcessoes(revisoes, leitor, relogio), new Usuario(PerfisDeTeste.Gestor), CancellationToken.None);
    }
}
