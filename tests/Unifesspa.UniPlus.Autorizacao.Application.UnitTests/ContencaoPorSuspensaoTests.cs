namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class ContencaoPorSuspensaoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Dados = "selecao:dados-pessoais:consultar";
    private const string EtapaGestao = "gestao_de_acessos";
    private const string EtapaProtecao = "protecao_de_dados";
    private const string Motivo = PerfisDeTeste.MotivoDaContencao;

    private static readonly TitularDaPermissao Rebeca = new("https://idp.test/realms/unifesspa", "rebeca.lima", "rebeca-lima12@uniplus-test.local");
    private static readonly Identidade Leandro = new("https://idp.test/realms/unifesspa", "leandro.viana", "Leandro Viana");
    private static readonly TitularDaPermissao Betina = new("https://idp.test/realms/unifesspa", "betina.neves", "betina.neves@uniplus-test.local");

    [Fact(DisplayName = "Quem mantém perfis suspende, o aviso sai para os outros titulares e a gravação é uma só")]
    public async Task Suspender_PelaGestao_GravaEAvisaOsOutros()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter], titulares: [Rebeca, Betina]);

        GravacaoDaOperacao<bool> gravacao = await Suspender(cenario, perfil);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        perfil.SuspensaoAberta!.Papel.Should().Be(EtapaDaVersao.GestaoDeAcessos);
        gravacao.Avisos.Select(a => a.Destinatario).Should().Equal(Rebeca.Email);
        gravacao.Avisos.Single().CorpoTexto.Should().NotContain(Motivo).And.NotContain("Betina");
        cenario.Unidade.Gravacoes.Should().Be(1);
        cenario.Verificador.Gravados.Should().ContainSingle();
    }

    [Fact(DisplayName = "O token sem nome de exibição grava a suspensão com nome nulo")]
    public async Task Suspender_TokenSemNome_GuardaNomeNulo()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter]);

        await Suspender(cenario, perfil);

        perfil.SuspensaoAberta!.SuspensaPor.Nome.Should().BeNull();
    }

    [Fact(DisplayName = "Quem aprova pela proteção de dados suspende com esse papel")]
    public async Task Suspender_PelaProtecaoDeDados_GuardaOPapel()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, [Protecao]);

        GravacaoDaOperacao<bool> gravacao = await Suspender(cenario, perfil);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        perfil.SuspensaoAberta!.Papel.Should().Be(EtapaDaVersao.ProtecaoDeDados);
    }

    [Fact(DisplayName = "Sem nenhuma das duas permissões a suspensão é negada, sem leitura do perfil e sem aviso")]
    public async Task Suspender_SemPermissao_Nega()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, [], titulares: [Rebeca]);

        GravacaoDaOperacao<bool> gravacao = await Suspender(cenario, perfil);

        Codigo(gravacao.Resultado).Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        gravacao.Avisos.Should().BeEmpty();
        perfil.SuspensaoAberta.Should().BeNull();
        cenario.Unidade.Gravacoes.Should().Be(0);
        cenario.Verificador.Gravados.Should().ContainSingle();
    }

    [Fact(DisplayName = "Sem identidade no token a suspensão não é registrada")]
    public async Task Suspender_SemAutor_Recusa()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, null, [Manter]);

        GravacaoDaOperacao<bool> gravacao = await Suspender(cenario, perfil);

        Codigo(gravacao.Resultado).Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
    }

    [Fact(DisplayName = "Suspender o perfil já suspenso é recusado e não gera outro aviso")]
    public async Task Suspender_JaSuspenso_Recusa()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter], titulares: [Rebeca]);
        await Suspender(cenario, perfil);

        GravacaoDaOperacao<bool> segunda = await Suspender(cenario, perfil);

        Codigo(segunda.Resultado).Should().Be(PerfilErrorCodes.JaSuspenso);
        segunda.Avisos.Should().BeEmpty();
        perfil.Suspensoes.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Suspender sem motivo é recusado e o perfil não muda")]
    public async Task Suspender_SemMotivo_Recusa()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter]);

        GravacaoDaOperacao<bool> gravacao = await cenario.Contencao.SuspenderAsync(perfil.Id, " ", cenario.Verificador, CancellationToken.None);

        Codigo(gravacao.Resultado).Should().Be(PerfilErrorCodes.MotivoInvalido);
        perfil.SuspensaoAberta.Should().BeNull();
    }

    [Fact(DisplayName = "Suspender o perfil que não existe devolve não encontrado")]
    public async Task Suspender_PerfilInexistente_NaoEncontrado()
    {
        CenarioDaGovernancaDaVersao cenario = new(Comum(), PerfisDeTeste.Gestora, [Manter]);

        GravacaoDaOperacao<bool> gravacao = await cenario.Contencao.SuspenderAsync(Guid.NewGuid(), Motivo, cenario.Verificador, CancellationToken.None);

        Codigo(gravacao.Resultado).Should().Be(PerfilErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "O handler da suspensão devolve a recusa como validação e leva os avisos junto")]
    public async Task HandlerDaSuspensao_DevolveRespostaEMensagens()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter], titulares: [Rebeca]);
        SuspenderPerfilCommand comando = new(perfil.Id, Motivo);

        (Result ok, IEnumerable<object> avisos) = await ContencaoPorSuspensaoCommandHandler.Handle(comando, cenario.Contencao, cenario.Verificador, CancellationToken.None);
        (Result recusa, IEnumerable<object> semAvisos) = await ContencaoPorSuspensaoCommandHandler.Handle(comando, cenario.Contencao, cenario.Verificador, CancellationToken.None);

        ok.IsSuccess.Should().BeTrue();
        avisos.Should().ContainSingle();
        recusa.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.JaSuspenso);
        semAvisos.Should().BeEmpty();
    }

    [Fact(DisplayName = "No perfil comum, a decisão da gestão por outra pessoa fecha a suspensão")]
    public async Task Retomar_PerfilComum_GestaoFecha()
    {
        Perfil perfil = SuspensoPor(Comum());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, [Manter]);

        Result<RetomadaDaSuspensaoDto> resultado = await Retomar(cenario, perfil, EtapaGestao);

        resultado.Value!.Concluida.Should().BeTrue();
        resultado.Value.EtapasPendentes.Should().BeEmpty();
        perfil.SuspensaoAberta.Should().BeNull();
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "No perfil protegido, a primeira etapa deixa a outra pendente e a segunda fecha")]
    public async Task Retomar_PerfilProtegido_DuasEtapas()
    {
        Perfil perfil = SuspensoPor(Protegido());
        CenarioDaGovernancaDaVersao gestao = new(perfil, PerfisDeTeste.ProtecaoDeDados, [Manter]);
        Result<RetomadaDaSuspensaoDto> primeira = await Retomar(gestao, perfil, EtapaGestao);
        CenarioDaGovernancaDaVersao protecao = new(perfil, Leandro, [Protecao]);

        Result<RetomadaDaSuspensaoDto> segunda = await Retomar(protecao, perfil, EtapaProtecao);

        primeira.Value!.Concluida.Should().BeFalse();
        primeira.Value.EtapasPendentes.Should().Equal(EtapaProtecao);
        segunda.Value!.Concluida.Should().BeTrue();
        perfil.SuspensaoAberta.Should().BeNull();
    }

    [Fact(DisplayName = "A concessão do decisor, lida sob a trava, fica na decisão")]
    public async Task Retomar_GuardaSeODecisorTinhaConcessao()
    {
        Perfil perfil = SuspensoPor(Comum());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, [Manter], temConcessao: true);

        await Retomar(cenario, perfil, EtapaGestao);

        perfil.Suspensoes.Single().Decisoes.Single().DecisorTemConcessao.Should().BeTrue();
    }

    [Theory(DisplayName = "A etapa ausente, desconhecida ou da outra permissão é recusada como etapa inválida")]
    [InlineData(null)]
    [InlineData("outra")]
    [InlineData(EtapaProtecao)]
    public async Task Retomar_EtapaQueNaoECabe_Recusa(string? etapa)
    {
        Perfil perfil = SuspensoPor(Protegido());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, [Manter]);

        Result<RetomadaDaSuspensaoDto> resultado = await Retomar(cenario, perfil, etapa);

        Codigo(resultado).Should().Be(PerfilErrorCodes.EtapaInvalida);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Sem nenhuma das permissões a retomada é negada")]
    public async Task Retomar_SemPermissao_Nega()
    {
        Perfil perfil = SuspensoPor(Comum());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, []);

        Result<RetomadaDaSuspensaoDto> resultado = await Retomar(cenario, perfil, EtapaGestao);

        Codigo(resultado).Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        cenario.Verificador.Gravados.Should().ContainSingle();
    }

    [Fact(DisplayName = "Quem suspendeu não retoma, mesmo com a permissão")]
    public async Task Retomar_PorQuemSuspendeu_Recusa()
    {
        Perfil perfil = SuspensoPor(Comum());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter]);

        Result<RetomadaDaSuspensaoDto> resultado = await Retomar(cenario, perfil, EtapaGestao);

        Codigo(resultado).Should().Be(PerfilErrorCodes.RetomadaPorQuemSuspendeu);
        perfil.SuspensaoAberta.Should().NotBeNull();
    }

    [Fact(DisplayName = "Retomar o perfil que não está suspenso é recusado")]
    public async Task Retomar_SemSuspensao_Recusa()
    {
        Perfil perfil = Comum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, [Manter]);

        Codigo(await Retomar(cenario, perfil, EtapaGestao)).Should().Be(PerfilErrorCodes.NaoSuspenso);
    }

    [Fact(DisplayName = "Sem identidade no token a retomada não é registrada")]
    public async Task Retomar_SemAutor_Recusa()
    {
        Perfil perfil = SuspensoPor(Comum());
        CenarioDaGovernancaDaVersao cenario = new(perfil, null, [Manter]);

        Codigo(await Retomar(cenario, perfil, EtapaGestao)).Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
    }

    [Fact(DisplayName = "O handler da retomada entrega o desfecho do caso de uso")]
    public async Task HandlerDaRetomada_EntregaODesfecho()
    {
        Perfil perfil = SuspensoPor(Comum());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, [Manter]);

        Result<RetomadaDaSuspensaoDto> resultado = await ContencaoPorSuspensaoCommandHandler.Handle(
            new RetomarSuspensaoDoPerfilCommand(perfil.Id, EtapaGestao, Motivo), cenario.Contencao, cenario.Verificador, CancellationToken.None);

        resultado.Value!.Concluida.Should().BeTrue();
    }

    private static Task<GravacaoDaOperacao<bool>> Suspender(CenarioDaGovernancaDaVersao cenario, Perfil perfil) =>
        cenario.Contencao.SuspenderAsync(perfil.Id, Motivo, cenario.Verificador, CancellationToken.None);

    private static Task<Result<RetomadaDaSuspensaoDto>> Retomar(CenarioDaGovernancaDaVersao cenario, Perfil perfil, string? etapa) =>
        cenario.Contencao.RetomarAsync(perfil.Id, etapa, Motivo, cenario.Verificador, CancellationToken.None);

    private static Perfil SuspensoPor(Perfil perfil)
    {
        perfil.Suspender(
            PerfisDeTeste.Gestora, EtapaDaVersao.GestaoDeAcessos, Motivo, PerfisDeTeste.Instante, new DoublesDePerfis.CatalogoDeTeste());

        return perfil;
    }

    private static Perfil Comum() => PerfisDeTeste.Vigente("Analista", "Descrição válida.", [Consultar], new DoublesDePerfis.CatalogoDeTeste());

    private static Perfil Protegido() => PerfisDeTeste.Vigente("Analista de dados", "Descrição válida.", [Consultar, Dados], new DoublesDePerfis.CatalogoDeTeste());

    private static string Codigo<T>(Result<T> resultado) => resultado.Errors.Single().Error.Code;
}
