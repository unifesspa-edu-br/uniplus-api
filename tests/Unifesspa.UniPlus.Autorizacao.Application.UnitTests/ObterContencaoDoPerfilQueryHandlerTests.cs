namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class ObterContencaoDoPerfilQueryHandlerTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Motivo = PerfisDeTeste.MotivoDaContencao;

    [Fact(DisplayName = "Quem mantém perfis lê a trilha: eventos e decisões, com a retomada atribuída ao sistema")]
    public async Task Obter_PorQuemMantem_DevolveATrilha()
    {
        Perfil perfil = Suspenso();
        perfil.DecidirRetomada(new ContextoDaRetomada(
            PerfisDeTeste.ProtecaoDeDados, true, EtapaDaVersao.GestaoDeAcessos, Motivo, new DoublesDePerfis.CatalogoDeTeste(), PerfisDeTeste.Instante));
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, [Manter]);

        Result<IReadOnlyList<RegistroDaContencaoDto>> resultado = await Obter(cenario, perfil.Id);

        resultado.Value!.Select(r => r.Tipo).Should().BeEquivalentTo("suspensao", "retomada", ContencaoMapping.TipoDaDecisao);
        resultado.Value!.Single(r => r.Tipo == ContencaoMapping.TipoDaDecisao).Should().Match<RegistroDaContencaoDto>(d =>
            d.Etapa == "gestao_de_acessos" && d.DecisorTemConcessao == true
            && d.Pessoa == new IdentidadeDto(PerfisDeTeste.ProtecaoDeDados.Emissor, PerfisDeTeste.ProtecaoDeDados.Sub, PerfisDeTeste.ProtecaoDeDados.Nome));
        resultado.Value!.Single(r => r.Tipo == "retomada").Should().Match<RegistroDaContencaoDto>(r =>
            r.Ator == "sistema" && r.Origem == "decisao_da_etapa" && r.Pessoa == null);
        cenario.Verificador.Gravados.Should().ContainSingle();
    }

    [Fact(DisplayName = "A suspensão por token sem nome traz o emissor e o sub de quem agiu, com o nome nulo")]
    public async Task Obter_SuspensaoSemNome_TrazEmissorESub()
    {
        Perfil perfil = Suspenso(new Identidade(PerfisDeTeste.Gestor.Emissor, PerfisDeTeste.Gestor.Sub, null));
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, [Manter]);

        Result<IReadOnlyList<RegistroDaContencaoDto>> resultado = await Obter(cenario, perfil.Id);

        resultado.Value!.Single().Pessoa.Should().Be(new IdentidadeDto(PerfisDeTeste.Gestor.Emissor, PerfisDeTeste.Gestor.Sub, null));
    }

    [Fact(DisplayName = "Quem aprova pela proteção de dados também lê a trilha")]
    public async Task Obter_PelaProtecaoDeDados_Le()
    {
        Perfil perfil = Suspenso();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, [Protecao]);

        (await Obter(cenario, perfil.Id)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Sem nenhuma das permissões a leitura é negada")]
    public async Task Obter_SemPermissao_Nega()
    {
        Perfil perfil = Suspenso();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, []);

        Result<IReadOnlyList<RegistroDaContencaoDto>> resultado = await Obter(cenario, perfil.Id);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        cenario.Verificador.Gravados.Should().ContainSingle();
    }

    [Fact(DisplayName = "O perfil que não existe devolve não encontrado")]
    public async Task Obter_PerfilInexistente_NaoEncontrado()
    {
        CenarioDaGovernancaDaVersao cenario = new(Suspenso(), PerfisDeTeste.Autor, [Manter]);

        Result<IReadOnlyList<RegistroDaContencaoDto>> resultado = await Obter(cenario, Guid.NewGuid());

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
    }

    private static Task<Result<IReadOnlyList<RegistroDaContencaoDto>>> Obter(CenarioDaGovernancaDaVersao cenario, Guid perfilId) =>
        ObterContencaoDoPerfilQueryHandler.Handle(new ObterContencaoDoPerfilQuery(perfilId), cenario.Verificador, cenario.Perfis, CancellationToken.None);

    private static Perfil Suspenso(Identidade? autor = null)
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", "Descrição válida.", [Consultar], new DoublesDePerfis.CatalogoDeTeste());
        perfil.Suspender(autor ?? PerfisDeTeste.Gestor, EtapaDaVersao.GestaoDeAcessos, Motivo, PerfisDeTeste.Instante, new DoublesDePerfis.CatalogoDeTeste());

        return perfil;
    }
}
