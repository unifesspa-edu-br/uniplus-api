namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class DetalheDoPerfilTests
{
    [Fact(DisplayName = "O detalhe de um perfil inexistente é recusado como não encontrado")]
    public async Task Montar_PerfilInexistente_RecusaComoNaoEncontrado()
    {
        Result<PerfilDto> resultado = await Detalhe(new PerfisNaMemoria(), Substitute.For<IJanelaDeMigracaoRepository>())
            .MontarAsync(Guid.CreateVersion7(), CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "O detalhe de um perfil sem janela de migração traz o perfil e a janela nula")]
    public async Task Montar_SemJanela_TrazOPerfilSemJanela()
    {
        Perfil perfil = PerfilVigente();

        Result<PerfilDto> resultado = await Detalhe(new PerfisNaMemoria(perfil), Substitute.For<IJanelaDeMigracaoRepository>())
            .MontarAsync(perfil.Id, CancellationToken.None);

        resultado.Value!.Should().Match<PerfilDto>(d => d.Id == perfil.Id && d.Nome == perfil.Nome && d.Janela == null);
    }

    [Fact(DisplayName = "O detalhe de um perfil com janela de migração aberta traz o resumo dela")]
    public async Task Montar_ComJanelaAberta_TrazOResumoDaJanela()
    {
        Perfil perfil = PerfilVigente();
        JanelaDeMigracao janela = JanelaDeMigracao.Abrir(perfil.Id, perfil.Versoes[^1], DateTimeOffset.UtcNow);
        IJanelaDeMigracaoRepository janelas = Substitute.For<IJanelaDeMigracaoRepository>();
        janelas.ObterUltimaParaLeituraAsync(perfil.Id, Arg.Any<CancellationToken>()).Returns(janela);
        janelas.TemJanelaAbertaAsync(perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(true);

        Result<PerfilDto> resultado = await Detalhe(new PerfisNaMemoria(perfil), janelas).MontarAsync(perfil.Id, CancellationToken.None);

        resultado.Value!.Janela.Should().Be(new JanelaDoPerfilDto(janela.Id, janela.Prazo, "aberta"));
    }

    private static Perfil PerfilVigente() =>
        PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["a:b:c"], new CatalogoDeTeste());

    private static DetalheDoPerfil Detalhe(IPerfilRepository perfis, IJanelaDeMigracaoRepository janelas)
    {
        LeitorDaMigracao leitor = new(janelas, Substitute.For<ICatalogoDePermissoesConsultavel>(), TimeProvider.System);

        return new DetalheDoPerfil(
            perfis, new CatalogoDaAplicacao([]), new LeituraDaJanela(janelas, leitor, TimeProvider.System), Substitute.For<IUserContext>());
    }
}
