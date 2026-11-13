namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A abertura de documento pela consulta administrativa exige a consulta da inscrição na unidade dona do processo antes da
/// permissão da classificação do documento. A recusa não distingue o que não existe do que a pessoa não pode abrir.
/// </summary>
public sealed class ObterAcessoAoDocumentoDaInscricaoQueryHandlerTests
{
    private static readonly string CodigoDeConsultarInscricoes = PermissaoSelecao.InscricoesConsultar.Codigo;
    private static readonly string CodigoDaClasse = PermissaoSelecao.DocumentosSensiveisConsultar.Codigo;
    private static readonly string CodigoDoArquivoNaoEncontrado = AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado.Code;

    private readonly IArquivoDoCandidatoStorage _storage = Substitute.For<IArquivoDoCandidatoStorage>();

    public ObterAcessoAoDocumentoDaInscricaoQueryHandlerTests() =>
        _storage.GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://armazenamento.exemplo/leitura");

    [Fact(DisplayName = "Com a consulta da inscrição e a permissão da classe do documento, Erick abre o laudo sensível de Fernanda")]
    public async Task ComConsultarEAClasse_Abre()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComLaudo();
        DecisoesDeAcessoDeTeste erick = new("erick.araujo", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        Result<AcessoArquivoDoCandidatoDto> resultado = await Abrir(cenario, await Mundo(cenario), erick.Verificador, cenario.ArquivoDe("LAUDO_MEDICO"));

        resultado.Value!.Url.Should().Be(new Uri("https://armazenamento.exemplo/leitura"));
    }

    [Fact(DisplayName = "Sem a consulta da inscrição, a abertura é recusada como a do documento inexistente e decide só a consulta da inscrição, que registra a negativa")]
    public async Task SemConsultarInscricoes_RecusaComoInexistente_RegistraANegativa()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComLaudo();
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.DocumentosSensiveisConsultar);

        Result<AcessoArquivoDoCandidatoDto> resultado = await Abrir(cenario, await Mundo(cenario), kevin.Verificador, cenario.ArquivoDe("LAUDO_MEDICO"));

        resultado.Error!.Code.Should().Be(CodigoDoArquivoNaoEncontrado);
        await kevin.ExigiuOAcessoAsync(cenario.InscricaoId, CodigoDeConsultarInscricoes);
        await kevin.NaoExigiuAsync(CodigoDaClasse);
        await _storage.DidNotReceiveWithAnyArgs().GerarUrlLeituraAsync(default!, default!, default, default);
    }

    [Fact(DisplayName = "Com a consulta da inscrição e sem a permissão da classe do documento, a abertura é recusada")]
    public async Task ComConsultarSemAClasse_Recusa()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComLaudo();
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosPessoaisConsultar);

        Result<AcessoArquivoDoCandidatoDto> resultado = await Abrir(cenario, await Mundo(cenario), kevin.Verificador, cenario.ArquivoDe("LAUDO_MEDICO"));

        resultado.Error!.Code.Should().Be(CodigoDoArquivoNaoEncontrado);
        await kevin.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoDaClasse), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceiveWithAnyArgs().GerarUrlLeituraAsync(default!, default!, default, default);
    }

    [Fact(DisplayName = "Integrante da banca com a permissão da classe e sem a consulta da inscrição é recusado ao abrir documento de inscrição de outro processo da unidade")]
    public async Task IntegranteSemConsultar_EmOutroProcessoDaUnidade_Recusado()
    {
        InscricaoAdministrativaDeTeste educacaoDoCampo = CenarioComLaudo(processoId: Guid.CreateVersion7());
        DecisoesDeAcessoDeTeste caroline = new("caroline.santos", PermissaoSelecao.DocumentosSensiveisConsultar);

        Result<AcessoArquivoDoCandidatoDto> resultado = await Abrir(
            educacaoDoCampo, await Mundo(educacaoDoCampo), caroline.Verificador, educacaoDoCampo.ArquivoDe("LAUDO_MEDICO"));

        resultado.Error!.Code.Should().Be(CodigoDoArquivoNaoEncontrado);
        await caroline.ExigiuOAcessoAsync(educacaoDoCampo.InscricaoId, CodigoDeConsultarInscricoes);
        await _storage.DidNotReceiveWithAnyArgs().GerarUrlLeituraAsync(default!, default!, default, default);
    }

    [Fact(DisplayName = "O arquivo que não existe é recusado sem decidir nem registrar nada")]
    public async Task ArquivoInexistente_NaoGravaDecisao()
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComLaudo();
        DecisoesDeAcessoDeTeste erick = new("erick.araujo", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        Result<AcessoArquivoDoCandidatoDto> resultado = await Abrir(cenario, await Mundo(cenario), erick.Verificador, Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be(CodigoDoArquivoNaoEncontrado);
        await erick.NaoExigiuNadaAsync();
        await erick.NaoRegistrouNadaAsync();
    }

    [Theory(DisplayName = "O impedimento e a identidade incompleta respondem o que são, e não a recusa do documento")]
    [InlineData(ResultadoDoAcesso.Impedido, ProcessoSeletivoErrorCodes.ImpedidoPorInscricaoNoProcesso)]
    [InlineData(ResultadoDoAcesso.IdentidadeIncompleta, ProcessoSeletivoErrorCodes.IdentidadeIncompleta)]
    public async Task ImpedimentoEIdentidadeIncompleta_RespondemOQueSao(ResultadoDoAcesso desfecho, string codigo)
    {
        InscricaoAdministrativaDeTeste cenario = CenarioComLaudo();
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(desfecho);

        Result<AcessoArquivoDoCandidatoDto> resultado = await Abrir(cenario, await Mundo(cenario), verificador, cenario.ArquivoDe("LAUDO_MEDICO"));

        resultado.Error!.Code.Should().Be(codigo);
        await _storage.DidNotReceiveWithAnyArgs().GerarUrlLeituraAsync(default!, default!, default, default);
    }

    private static InscricaoAdministrativaDeTeste CenarioComLaudo(Guid? processoId = null) =>
        new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond, processoId: processoId)
            .Exigencia("LAUDO_MEDICO", ClassificacaoDeProtecao.Sensivel);

    private static async Task<InscricaoAdministrativaDeTeste.Mundo> Mundo(InscricaoAdministrativaDeTeste cenario)
    {
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();
        foreach (ArquivoDaApresentacao arquivo in await mundo.Documentos.ListarArquivosDaInscricaoAsync(cenario.InscricaoId, CancellationToken.None))
        {
            mundo.Documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);
        }

        foreach (ApresentacaoDeDocumento apresentacao in await mundo.Documentos.ListarApresentacoesDaInscricaoAsync(cenario.InscricaoId, CancellationToken.None))
        {
            mundo.Documentos.ObterApresentacaoAsync(apresentacao.Id, Arg.Any<CancellationToken>()).Returns(apresentacao);
        }

        return mundo;
    }

    private Task<Result<AcessoArquivoDoCandidatoDto>> Abrir(
        InscricaoAdministrativaDeTeste cenario, InscricaoAdministrativaDeTeste.Mundo mundo, IVerificadorDeAcesso verificador, Guid arquivoId)
    {
        ClassificacaoDaExigenciaDoArquivo classificacao = new(mundo.Documentos, mundo.Processos, mundo.Codecs);
        DecisaoDoAcessoAoDocumento acesso = new(
            verificador, new PortaDePermissaoPorPar<PermissaoSelecao>(), mundo.Processos, mundo.Inscricoes, mundo.Documentos, classificacao);

        return ObterAcessoAoDocumentoDaInscricaoQueryHandler.Handle(
            new ObterAcessoAoDocumentoDaInscricaoQuery(cenario.ProcessoId, cenario.InscricaoId, arquivoId),
            acesso,
            new EmissaoDoAcessoAoArquivo(mundo.Documentos, _storage, new RelogioFixo(DateTimeOffset.UnixEpoch)),
            Substitute.For<IUserContext>(),
            NullLogger.Instance,
            CancellationToken.None);
    }
}
