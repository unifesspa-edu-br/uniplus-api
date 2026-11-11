namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
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
        VerificadorDeTeste verificador = new(PerfisDeTeste.Autor.Sub, []);

        Result<ListarPerfisResult> resultado = await ListarPerfisQueryHandler.Handle(
            new ListarPerfisQuery(null, 10, default), verificador, perfis, Catalogo, CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.LeituraNegada);
        perfis.ReceivedCalls().Should().BeEmpty();
        TrilhaDeAcesso.Do(verificador).Should().BeEquivalentTo(Negativas(ResourceTipos.Operacao, null));
    }

    [Fact(DisplayName = "A leitura recusada de um perfil não consulta o perfil nem a janela de migração")]
    public async Task Obter_SemPermissao_RecusaSemConsultarOPerfilNemAJanela()
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IJanelaDeMigracaoRepository janelasDoBanco = Substitute.For<IJanelaDeMigracaoRepository>();
        VerificadorDeTeste verificador = new(PerfisDeTeste.Autor.Sub, []);
        Guid perfilId = Guid.CreateVersion7();

        Result<PerfilDto> resultado = await ObterPerfilQueryHandler.Handle(new ObterPerfilQuery(perfilId), verificador, Detalhe(perfis, janelasDoBanco), CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.LeituraNegada);
        perfis.ReceivedCalls().Should().BeEmpty();
        janelasDoBanco.ReceivedCalls().Should().BeEmpty();
        TrilhaDeAcesso.Do(verificador).Should().BeEquivalentTo(Negativas(ResourceTiposAutorizacao.Perfil, perfilId));
    }

    private static IEnumerable<TrilhaDeAcesso> Negativas(string resourceTipo, Guid? resourceId) =>
        new[] { Manter, Gestao, Protecao }.Select(p => new TrilhaDeAcesso(PerfisDeTeste.Autor.Sub, p, resourceTipo, resourceId, ResultadoDoAcesso.Negado));

    private static DetalheDoPerfil Detalhe(IPerfilRepository perfis, IJanelaDeMigracaoRepository janelasDoBanco)
    {
        LeitorDaMigracao leitor = new(janelasDoBanco, Substitute.For<ICatalogoDePermissoesConsultavel>(), TimeProvider.System);

        return new DetalheDoPerfil(perfis, Catalogo, new LeituraDaJanela(janelasDoBanco, leitor, TimeProvider.System), Substitute.For<IUserContext>());
    }
}
