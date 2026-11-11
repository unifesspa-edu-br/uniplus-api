namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class LeituraDoPerfilRecusadaTests
{
    private static readonly CatalogoDaAplicacao Catalogo = new([]);

    [Fact(DisplayName = "A listagem recusada não consulta os perfis")]
    public async Task Listar_SemPermissao_RecusaSemConsultarOsPerfis()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();

        Result<ListarPerfisResult> resultado = await ListarPerfisQueryHandler.Handle(
            new ListarPerfisQuery(null, 10, default), new VerificadorDeTeste(PerfisDeTeste.Autor.Sub, []), perfis, Catalogo, CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.LeituraNegada);
        perfis.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "A leitura recusada de um perfil não consulta o perfil nem a janela de migração")]
    public async Task Obter_SemPermissao_RecusaSemConsultarOPerfilNemAJanela()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IJanelaDeMigracaoRepository janelasDoBanco = Substitute.For<IJanelaDeMigracaoRepository>();
        LeituraDaJanela janelas = new(
            janelasDoBanco,
            new LeitorDaMigracao(janelasDoBanco, Substitute.For<ICatalogoDePermissoesConsultavel>(), TimeProvider.System),
            TimeProvider.System);

        Result<PerfilDto> resultado = await ObterPerfilQueryHandler.Handle(
            new ObterPerfilQuery(Guid.NewGuid()),
            new VerificadorDeTeste(PerfisDeTeste.Autor.Sub, []),
            perfis,
            Catalogo,
            janelas,
            Substitute.For<IUserContext>(),
            CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.LeituraNegada);
        perfis.ReceivedCalls().Should().BeEmpty();
        janelasDoBanco.ReceivedCalls().Should().BeEmpty();
    }
}
