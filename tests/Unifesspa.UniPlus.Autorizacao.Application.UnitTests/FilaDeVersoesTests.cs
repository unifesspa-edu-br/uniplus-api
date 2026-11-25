namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class FilaDeVersoesTests
{
    [Fact(DisplayName = "A fila da gestão lista a versão pendente com o documento verificado")]
    public async Task Fila_Gestao_ListaAPendente()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestor, [Gestao]);

        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> fila = await Fila(cenario, EtapaGestao);
        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> daProtecao = await Fila(cenario, EtapaProtecao);

        fila.Value!.Should().ContainSingle().Which.PerfilId.Should().Be(perfil.Id);
        fila.Value!.Single().PropostaPorQuemLe.Should().BeFalse();
        daProtecao.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
    }

    [Fact(DisplayName = "A fila marca como proposta por quem lê a versão que o leitor criou")]
    public async Task Fila_MarcaAVersaoDoProprioAutor()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, [Gestao]);

        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> fila = await Fila(cenario, EtapaGestao, PerfisDeTeste.Autor);

        ItemDaFilaDeVersoesDto item = fila.Value!.Should().ContainSingle().Subject;
        item.PropostaPorQuemLe.Should().BeTrue();
        item.Versao.PropostaPorQuemLe.Should().BeTrue();
    }

    [Theory(DisplayName = "A fila de etapa desconhecida é recusada")]
    [InlineData(null)]
    [InlineData("outra")]
    public async Task Fila_EtapaInvalida_Recusa(string? etapa)
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestor, [Gestao]);

        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> fila = await Fila(cenario, etapa);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.EtapaInvalida);
    }

    [Theory(DisplayName = "A fila de versões recusada registra a negativa da permissão da etapa na operação")]
    [InlineData(EtapaGestao, Gestao, Protecao)]
    [InlineData(EtapaProtecao, Protecao, Gestao)]
    public async Task Fila_SemPermissaoDaEtapa_RegistraANegativaDaEtapa(string etapa, string permissaoDaEtapa, string deOutraEtapa)
    {
        CenarioDaGovernancaDaVersao cenario = new(PendenteVerificada([A]), PerfisDeTeste.Gestor, [deOutraEtapa]);

        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> fila = await Fila(cenario, etapa);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(cenario.Verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Negado));
    }

    [Theory(DisplayName = "A fila de versões permitida registra a decisão da permissão da etapa na operação")]
    [InlineData(EtapaGestao, Gestao)]
    [InlineData(EtapaProtecao, Protecao)]
    public async Task Fila_ComPermissaoDaEtapa_RegistraAPermitida(string etapa, string permissaoDaEtapa)
    {
        CenarioDaGovernancaDaVersao cenario = new(PendenteVerificada([A]), PerfisDeTeste.Gestor, [permissaoDaEtapa]);

        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> fila = await Fila(cenario, etapa);

        fila.IsSuccess.Should().BeTrue();
        TrilhaDeAcesso.Do(cenario.Verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Permitido));
    }

    [Theory(DisplayName = "A fila de versões recusa o token sem autor sem consultar os perfis nem registrar")]
    [InlineData(EtapaGestao)]
    [InlineData(EtapaProtecao)]
    public async Task Fila_SemAutor_RecusaPeloTokenSemConsultarNemRegistrar(string etapa)
    {
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IVerificadorDeAcesso verificador = SemAutor();

        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> fila = await ListarFilaDeVersoesQueryHandler.Handle(
            new ListarFilaDeVersoesQuery(etapa), verificador, new FilaDeVersoesDaEtapa(perfis, new CatalogoDaAplicacao([]), TimeProvider.System),
            new Usuario(PerfisDeTeste.Gestor), CancellationToken.None);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        perfis.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    private static IVerificadorDeAcesso SemAutor()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));
        return verificador;
    }

    private static TrilhaDeAcesso NaOperacao(string permissao, ResultadoDoAcesso resultado) =>
        new(PerfisDeTeste.Gestor.ContaId, permissao, ResourceTipos.Operacao, null, resultado);

    private static Task<Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>> Fila(
        CenarioDaGovernancaDaVersao cenario, string? etapa, Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects.Identidade? leitor = null) =>
        ListarFilaDeVersoesQueryHandler.Handle(
            new ListarFilaDeVersoesQuery(etapa),
            cenario.Verificador,
            new FilaDeVersoesDaEtapa(
                cenario.Perfis,
                new Unifesspa.UniPlus.Authorization.Catalogo.CatalogoDaAplicacao([]),
                new RelogioFixo(PerfisDeTeste.Instante.AddHours(1))),
            new DoublesDePerfis.Usuario(leitor ?? PerfisDeTeste.Gestor),
            CancellationToken.None);

}
