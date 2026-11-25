namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class DetalheDoPerfilTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "O detalhe de um perfil inexistente é recusado como não encontrado")]
    public async Task Montar_PerfilInexistente_RecusaComoNaoEncontrado()
    {
        Result<PerfilDto> resultado = await Detalhe(new PerfisNaMemoria(), Substitute.For<IRevisaoDasConcessoesRepository>())
            .MontarAsync(Guid.CreateVersion7(), CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "O detalhe de um perfil sem revisão das concessões traz o perfil e a revisão nula")]
    public async Task Montar_SemRevisao_TrazOPerfilSemRevisao()
    {
        Perfil perfil = PerfilVigente();

        Result<PerfilDto> resultado = await Detalhe(new PerfisNaMemoria(perfil), Substitute.For<IRevisaoDasConcessoesRepository>())
            .MontarAsync(perfil.Id, CancellationToken.None);

        resultado.Value!.Should().Match<PerfilDto>(d => d.Id == perfil.Id && d.Nome == perfil.Nome && d.RevisaoDasConcessoes == null);
    }

    [Fact(DisplayName = "O detalhe de um perfil com revisão das concessões aberta traz o resumo dela")]
    public async Task Montar_ComRevisaoAberta_TrazOResumoDaRevisao()
    {
        Perfil perfil = PerfilVigente();
        RevisaoDasConcessoesDoPerfil revisao = RevisaoDasConcessoesDoPerfil.Abrir(perfil.Id, perfil.Versoes[^1], Agora);
        IRevisaoDasConcessoesRepository revisoes = Substitute.For<IRevisaoDasConcessoesRepository>();
        revisoes.ObterUltimaParaLeituraAsync(perfil.Id, Arg.Any<CancellationToken>()).Returns(revisao);
        revisoes.TemRevisaoAbertaAsync(perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(true);

        Result<PerfilDto> resultado = await Detalhe(new PerfisNaMemoria(perfil), revisoes).MontarAsync(perfil.Id, CancellationToken.None);

        resultado.Value!.RevisaoDasConcessoes.Should().Be(new RevisaoDoPerfilDto(revisao.Id, revisao.TerminaEm, CodigoComRotulo<SituacaoDaRevisaoDasConcessoes>.De(SituacaoDaRevisaoDasConcessoes.Aberta)));
    }

    [Fact(DisplayName = "O detalhe de um perfil com revisão em encerramento traz o resumo dela")]
    public async Task Montar_ComRevisaoEmEncerramento_TrazOResumoDaRevisao()
    {
        Perfil perfil = PerfilVigente();
        RevisaoDasConcessoesDoPerfil revisao = RevisaoDasConcessoesDoPerfil.Abrir(perfil.Id, perfil.Versoes[^1], Agora);
        IRevisaoDasConcessoesRepository revisoes = Substitute.For<IRevisaoDasConcessoesRepository>();
        revisoes.ObterUltimaParaLeituraAsync(perfil.Id, Arg.Any<CancellationToken>()).Returns(revisao);
        revisoes.TemRevisaoAbertaAsync(perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(false);

        Result<PerfilDto> resultado = await Detalhe(new PerfisNaMemoria(perfil), revisoes).MontarAsync(perfil.Id, CancellationToken.None);

        resultado.Value!.RevisaoDasConcessoes!.Situacao.Codigo.Should().Be("ENCERRANDO");
    }

    private static Perfil PerfilVigente() =>
        PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["a:b:c"], new CatalogoDeTeste());

    private static DetalheDoPerfil Detalhe(IPerfilRepository perfis, IRevisaoDasConcessoesRepository revisoes)
    {
        TimeProvider relogio = Substitute.For<TimeProvider>();
        relogio.GetUtcNow().Returns(Agora);
        LeitorDaMigracao leitor = new(revisoes, Substitute.For<ICatalogoDePermissoesConsultavel>(), relogio);

        return new DetalheDoPerfil(
            perfis, new CatalogoDaAplicacao([]), new LeituraDaRevisaoDasConcessoes(revisoes, leitor, relogio), Substitute.For<IUserContext>());
    }
}
