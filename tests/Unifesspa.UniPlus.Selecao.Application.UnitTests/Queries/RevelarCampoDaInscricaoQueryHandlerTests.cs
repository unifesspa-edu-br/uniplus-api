namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.CampoDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A revelação devolve o CPF no formato de CPF e trata o valor decifrado vazio como campo não encontrado.</summary>
public sealed class RevelarCampoDaInscricaoQueryHandlerTests
{
    private static readonly string CodigoRevelar = PermissaoSelecao.DadosIdentificadoresRevelar.Codigo;
    private static readonly string CodigoDaClasse = PermissaoSelecao.DadosIdentificadoresConsultar.Codigo;

    [Fact(DisplayName = "O fato de formato CPF é revelado formatado")]
    public async Task Revela_Cpf_Formatado()
    {
        Result<CampoReveladoDto> resultado = await Revelar(PersonasDeTeste.FernandaDrumond.Valor);

        resultado.Value!.Valor.Should().Be(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Theory(DisplayName = "O valor decifrado nulo ou vazio responde campo não encontrado, e não erro do servidor")]
    [InlineData(null)]
    [InlineData("")]
    public async Task ValorVazio_NaoEncontrado(string? decifrado)
    {
        Result<CampoReveladoDto> resultado = await Revelar(decifrado);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Inscrição que não existe, pedida sem a permissão de revelar, responde 404 e registra a negativa sem o id do resource")]
    public async Task Inexistente_SemAPermissao_RegistraANegativaSemId()
    {
        InscricaoAdministrativaDeTeste cenario = new(PersonasDeTeste.FernandaDrumond);
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.DadosPessoaisConsultar);

        Result<CampoReveladoDto> resultado = await RevelarCampoDaInscricaoQueryHandler.Handle(
            new RevelarCampoDaInscricaoQuery(cenario.ProcessoId, Guid.CreateVersion7(), "CPF", null),
            kevin.Verificador, mundo.Processos, mundo.Inscricoes, mundo.Codecs,
            Substitute.For<IUserContext>(), NullLogger.Instance, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        IReadOnlyCollection<DecisaoDeAcesso> entregues = ((IReadOnlyCollection<DecisaoDeAcesso>)kevin.Verificador.ReceivedCalls()
            .Single(static c => c.GetMethodInfo().Name == nameof(IVerificadorDeAcesso.RegistrarDesfechoAsync)).GetArguments()[0]!);
        DecisaoNegada negada = entregues.Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>().Subject;
        negada.Registro.Permissao.Should().Be(PermissaoSelecao.DadosIdentificadoresRevelar.Codigo);
        negada.Registro.ResourceId.Should().BeNull();
        negada.Registro.FatoCodigo.Should().BeNull();
    }

    [Theory(DisplayName = "Sem a permissão de revelar, a resposta existente e a ausente dão o mesmo 404, decidido antes de procurar a resposta")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SemRevelar_MesmoNaoEncontrado_AntesDeProcurarAResposta(bool respostaExiste)
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();
        DecisoesDeAcessoDeTeste kevin = new(
            "kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);
        if (respostaExiste)
        {
            mundo.Inscricoes.LocalizarIdentificadorAsync(cenario.InscricaoId, "CPF", null, null, Arg.Any<CancellationToken>())
                .Returns(new RespostaIdentificadora(Guid.CreateVersion7()));
        }

        Result<CampoReveladoDto> resultado = await Revelar(cenario, mundo, kevin);

        resultado.Error!.Code.Should().Be("Inscricao.CampoNaoEncontrado");
        await kevin.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoRevelar),
            Arg.Is<ResourceContext?>(r => r!.FatoCodigo == "CPF" && r.InscricaoId == cenario.InscricaoId),
            Arg.Any<CancellationToken>());
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().LocalizarIdentificadorAsync(default, default!, default, default, default);
    }

    [Fact(DisplayName = "Erick com a permissão de revelar e a do identificador mascarado, sem consultar inscrições, revela o CPF")]
    public async Task ComAsDuasPermissoes_Revela()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        InscricaoAdministrativaDeTeste.Mundo mundo = Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor);
        DecisoesDeAcessoDeTeste erick = new("erick.araujo", PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, mundo, erick);

        resultado.Value!.Valor.Should().Be(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Fact(DisplayName = "Erick só com a permissão de revelar, sem a do identificador mascarado, não revela o CPF: 404, só a negativa da classe é registrada e a resposta nem é procurada")]
    public async Task SoRevelar_SemAClasse_Recusa()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        InscricaoAdministrativaDeTeste.Mundo mundo = Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor);
        DecisoesDeAcessoDeTeste erick = new("erick.araujo", PermissaoSelecao.DadosIdentificadoresRevelar);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, mundo, erick);

        resultado.Error!.Code.Should().Be("Inscricao.CampoNaoEncontrado");
        await erick.Verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d =>
                d.Count == 1 && d.Single() is DecisaoNegada && d.Single().Resultado == ResultadoDoAcesso.Negado
                && ((DecisaoNegada)d.Single()).Requisito.Permissao == CodigoDaClasse),
            IntencaoDoRegistro.Pedido,
            Arg.Any<CancellationToken>());
        await erick.Verificador.DidNotReceive().ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoRevelar), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().LocalizarIdentificadorAsync(default, default!, default, default, default);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().LerIdentificadorAsync(default, default);
    }

    private static InscricaoAdministrativaDeTeste CenarioComCpf() =>
        new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("CPF", "IDENTIFICADOR", TipoRenderizacao.Texto, null, formato: "CPF");

    private static InscricaoAdministrativaDeTeste.Mundo Mundo(InscricaoAdministrativaDeTeste cenario, string? decifrado)
    {
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();
        mundo.Inscricoes.LocalizarIdentificadorAsync(cenario.InscricaoId, "CPF", null, null, Arg.Any<CancellationToken>())
            .Returns(new RespostaIdentificadora(Guid.CreateVersion7()));
        mundo.Inscricoes.LerIdentificadorAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(decifrado);

        return mundo;
    }

    private static Task<Result<CampoReveladoDto>> Revelar(string? decifrado)
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();

        return Revelar(cenario, Mundo(cenario, decifrado), new DecisoesDeAcessoDeTeste("diogo.souza", PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar));
    }

    private static async Task<Result<CampoReveladoDto>> Revelar(
        InscricaoAdministrativaDeTeste cenario, InscricaoAdministrativaDeTeste.Mundo mundo, DecisoesDeAcessoDeTeste persona)
    {
        IUserContext usuario = Substitute.For<IUserContext>();

        return await RevelarCampoDaInscricaoQueryHandler.Handle(
            new RevelarCampoDaInscricaoQuery(cenario.ProcessoId, cenario.InscricaoId, "CPF", null),
            persona.Verificador, mundo.Processos, mundo.Inscricoes, mundo.Codecs, usuario, NullLogger.Instance, CancellationToken.None);
    }
}
