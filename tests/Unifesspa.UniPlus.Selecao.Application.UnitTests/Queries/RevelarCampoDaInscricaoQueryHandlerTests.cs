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
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A revelação administrativa de um campo identificador exige, nesta ordem, a consulta da inscrição, a permissão da classe
/// e a de revelar; sem qualquer uma, a resposta existente e a ausente dão o mesmo 404, e a resposta nem é procurada.
/// </summary>
public sealed class RevelarCampoDaInscricaoQueryHandlerTests
{
    private const string CodigoDoCampoNaoEncontrado = "Inscricao.CampoNaoEncontrado";

    private static readonly string CodigoDeConsultarInscricoes = PermissaoSelecao.InscricoesConsultar.Codigo;
    private static readonly string CodigoDaClasse = PermissaoSelecao.DadosIdentificadoresConsultar.Codigo;
    private static readonly string CodigoRevelar = PermissaoSelecao.DadosIdentificadoresRevelar.Codigo;

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

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
    }

    [Fact(DisplayName = "Erick com a consulta da inscrição, a permissão do identificador mascarado e a de revelar revela o CPF")]
    public async Task ComAsTresPermissoes_Revela()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        DecisoesDeAcessoDeTeste erick = Persona("erick.araujo", consultar: true, classe: true, revelar: true);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor), erick.Verificador);

        resultado.Value!.Valor.Should().Be(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Theory(DisplayName = "Sem a consulta da inscrição, responde 404 e decide só a consulta, sobre o resource da inscrição, que registra a negativa")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SemConsultarInscricoes_NaoEncontrado_RegistraANegativaDaConsulta(bool respostaExiste)
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        InscricaoAdministrativaDeTeste.Mundo mundo = respostaExiste ? Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor) : cenario.Montar();
        DecisoesDeAcessoDeTeste kevin = Persona("kevin.carvalho", consultar: false, classe: true, revelar: true);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, mundo, kevin.Verificador);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await kevin.ExigiuOAcessoAsync(cenario.InscricaoId, CodigoDeConsultarInscricoes);
        await kevin.NaoExigiuAsync(CodigoRevelar);
        await kevin.NaoAvaliouNadaAsync();
    }

    [Fact(DisplayName = "Sem a consulta da inscrição, o campo existente e o ausente respondem exatamente o mesmo erro")]
    public async Task SemConsultarInscricoes_CampoExistenteEAusente_RespondemIgual()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        Task<Result<CampoReveladoDto>> existente = Revelar(cenario, Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor), SemConsultarInscricoes().Verificador);
        Task<Result<CampoReveladoDto>> ausente = Revelar(cenario, cenario.Montar(), SemConsultarInscricoes().Verificador);

        (await existente).Error.Should().Be((await ausente).Error);
    }

    [Fact(DisplayName = "Erick só com a permissão do identificador mascarado não revela: 404, sem decidir revelar e sem procurar a resposta")]
    public async Task SoAClasse_NaoRevela()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        InscricaoAdministrativaDeTeste.Mundo mundo = Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor);
        DecisoesDeAcessoDeTeste erick = Persona("erick.araujo", consultar: false, classe: true, revelar: false);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, mundo, erick.Verificador);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await erick.NaoExigiuAsync(CodigoRevelar);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().LocalizarIdentificadorAsync(default, default!, default, default, default);
    }

    [Fact(DisplayName = "Com a consulta da inscrição e a do identificador mascarado, sem a de revelar, responde 404 decidido antes de procurar a resposta")]
    public async Task SemRevelar_NaoEncontrado_AntesDeProcurarAResposta()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        InscricaoAdministrativaDeTeste.Mundo mundo = Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor);
        DecisoesDeAcessoDeTeste kevin = Persona("kevin.carvalho", consultar: true, classe: true, revelar: false);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, mundo, kevin.Verificador);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await kevin.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoRevelar),
            Arg.Is<ResourceContext?>(r => r!.FatoCodigo == "CPF" && r.InscricaoId == cenario.InscricaoId),
            Arg.Any<CancellationToken>());
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().LocalizarIdentificadorAsync(default, default!, default, default, default);
    }

    [Fact(DisplayName = "Com a consulta da inscrição e a de revelar, sem a do identificador mascarado, não revela: 404, só a negativa da classe é registrada e a resposta nem é procurada")]
    public async Task SoRevelar_SemAClasse_Recusa()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        InscricaoAdministrativaDeTeste.Mundo mundo = Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor);
        DecisoesDeAcessoDeTeste erick = Persona("erick.araujo", consultar: true, classe: false, revelar: true);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, mundo, erick.Verificador);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        erick.DecisoesRegistradas().Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>().Subject.Registro.Permissao.Should().Be(CodigoDaClasse);
        await erick.NaoExigiuAsync(CodigoRevelar);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().LerIdentificadorAsync(default, default);
    }

    [Theory(DisplayName = "Inscrição que não existe, pedida sem uma das três permissões, responde 404 e registra só a primeira negada, sem o id do resource")]
    [InlineData(false, true, true, "selecao:inscricoes:consultar")]
    [InlineData(true, false, true, "selecao:dados-identificadores:consultar")]
    [InlineData(true, true, false, "selecao:dados-identificadores:revelar")]
    public async Task Inexistente_SemAPermissao_RegistraAPrimeiraNegadaSemId(bool consultar, bool classe, bool revelar, string negada)
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        DecisoesDeAcessoDeTeste kevin = Persona("kevin.carvalho", consultar, classe, revelar);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, cenario.Montar(), kevin.Verificador, Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        DecisaoNegada registrada = kevin.DecisoesRegistradas().Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>().Subject;
        registrada.Registro.Permissao.Should().Be(negada);
        registrada.Registro.ResourceId.Should().BeNull();
        registrada.Registro.FatoCodigo.Should().BeNull();
        registrada.Registro.MotivoNegativa.Should().Be("sem_concessao_aplicavel");
    }

    [Fact(DisplayName = "Inscrição que não existe, pedida por quem tem as três permissões, responde 404 sem registrar nada")]
    public async Task Inexistente_ComAsTresPermissoes_NaoRegistra()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        DecisoesDeAcessoDeTeste erick = Persona("erick.araujo", consultar: true, classe: true, revelar: true);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, cenario.Montar(), erick.Verificador, Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await erick.NaoRegistrouNadaAsync();
    }

    [Theory(DisplayName = "O impedimento e a identidade incompleta respondem o que são, e não o 404 do campo")]
    [InlineData(ResultadoDoAcesso.Impedido, ProcessoSeletivoErrorCodes.ImpedidoPorInscricaoNoProcesso)]
    [InlineData(ResultadoDoAcesso.IdentidadeIncompleta, ProcessoSeletivoErrorCodes.IdentidadeIncompleta)]
    public async Task ImpedimentoEIdentidadeIncompleta_RespondemOQueSao(ResultadoDoAcesso desfecho, string codigo)
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComCpf();
        InscricaoAdministrativaDeTeste.Mundo mundo = Mundo(cenario, PersonasDeTeste.FernandaDrumond.Valor);
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(desfecho);

        Result<CampoReveladoDto> resultado = await Revelar(cenario, mundo, verificador);

        resultado.Error!.Code.Should().Be(codigo);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().LocalizarIdentificadorAsync(default, default!, default, default, default);
    }

    private static DecisoesDeAcessoDeTeste Persona(string nome, bool consultar, bool classe, bool revelar) =>
        new(
            nome,
            [
                .. consultar ? [PermissaoSelecao.InscricoesConsultar] : Array.Empty<PermissaoSelecao>(),
                .. classe ? [PermissaoSelecao.DadosIdentificadoresConsultar] : Array.Empty<PermissaoSelecao>(),
                .. revelar ? [PermissaoSelecao.DadosIdentificadoresRevelar] : Array.Empty<PermissaoSelecao>(),
            ]);

    private static DecisoesDeAcessoDeTeste SemConsultarInscricoes() => Persona("kevin.carvalho", consultar: false, classe: true, revelar: true);

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

        return Revelar(cenario, Mundo(cenario, decifrado), Persona("diogo.souza", consultar: true, classe: true, revelar: true).Verificador);
    }

    private static Task<Result<CampoReveladoDto>> Revelar(
        InscricaoAdministrativaDeTeste cenario, InscricaoAdministrativaDeTeste.Mundo mundo, IVerificadorDeAcesso verificador, Guid? inscricaoId = null)
    {
        LeituraDaInscricaoAdministrativa leitura = new(mundo.Processos, mundo.Inscricoes, mundo.Documentos, mundo.Codecs);

        return RevelarCampoDaInscricaoQueryHandler.Handle(
            new RevelarCampoDaInscricaoQuery(cenario.ProcessoId, inscricaoId ?? cenario.InscricaoId, "CPF", null),
            new AcessoARevelacaoDoCampo(verificador, mundo.Processos, mundo.Inscricoes),
            new LeituraDoIdentificador(leitura, mundo.Inscricoes),
            Substitute.For<IUserContext>(),
            NullLogger.Instance,
            CancellationToken.None);
    }
}
