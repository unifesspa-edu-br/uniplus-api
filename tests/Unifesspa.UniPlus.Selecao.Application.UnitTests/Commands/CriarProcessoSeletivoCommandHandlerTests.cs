namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CriarProcessoSeletivoCommandHandlerTests
{
    private static readonly Guid UnidadeId = Guid.NewGuid();

    private static readonly UnidadeView UnidadeCeps = new(
        UnidadeId, "CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null,
        "1504208", "Marabá", "PA");

    private static readonly TipoProcessoView TipoSisu = new(
        TiposDeProcessoDeTeste.SiSU.OrigemId, "SiSU", "SiSU", null);

    private readonly IProcessoSeletivoRepository _repository = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IUnidadeReader _unidadeReader = Substitute.For<IUnidadeReader>();
    private readonly ITipoProcessoReader _tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IVerificadorDeAcesso _verificador = Substitute.For<IVerificadorDeAcesso>();
    private ProcessoSeletivo? _persistido;

    public CriarProcessoSeletivoCommandHandlerTests()
    {
        _unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        _tipoProcessoReader.ObterAtivoPorIdAsync(TiposDeProcessoDeTeste.SiSU.OrigemId, Arg.Any<CancellationToken>()).Returns(TipoSisu);
        _verificador.ExigirAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);
        _repository.When(r => r.AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>()))
            .Do(chamada => _persistido = chamada.Arg<ProcessoSeletivo>());
    }

    private Task<Result<Guid>> CriarAsync(CriarProcessoSeletivoCommand comando) =>
        CriarProcessoSeletivoCommandHandler.Handle(comando, _repository, _verificador, _unidadeReader, _tipoProcessoReader, _unitOfWork, CancellationToken.None);

    [Fact]
    public async Task Handle_ComDadosValidos_PersisteRascunhoEDevolveIdentificador()
    {
        Result<Guid> resultado = await CriarAsync(new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir());

        resultado.IsSuccess.Should().BeTrue();
        _persistido.Should().NotBeNull();
        resultado.Value.Should().Be(_persistido!.Id);
        _persistido.Nome.Should().Be("PS 2026 — SiSU");
        _persistido.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Rascunho);
        await _repository.Received(1).AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ComDadosValidos_CopiaUnidadeAdministradora()
    {
        await CriarAsync(new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir());

        _persistido!.UnidadeAdministradora.Should().BeEquivalentTo(new
        {
            Codigo = "CEPS",
            Sigla = "CEPS",
            IdentificadorNoEnderecoWeb = "ceps",
            Nome = "Centro de Processos Seletivos",
            Tipo = "ADMINISTRATIVA",
            CidadeCodigoIbge = "1504208",
            CidadeNome = "Marabá",
            CidadeUf = "PA"
        });
        _persistido.UnidadeAdministradoraOrigemId.Should().Be(UnidadeId);
    }

    [Fact]
    public async Task Handle_ComDadosValidos_VinculaTipoDoProcesso()
    {
        await CriarAsync(new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir());

        _persistido!.TipoProcessoOrigemId.Should().Be(TipoSisu.Id);
        _persistido.TipoProcesso.Codigo.Should().Be("SiSU");
        _persistido.TipoProcesso.Nome.Should().Be("SiSU");
    }

    [Fact(DisplayName = "Handle recusa quando a Unidade administradora não é encontrada e não persiste nada (CA-02)")]
    public async Task Handle_UnidadeNaoEncontrada_RecusaSemPersistir()
    {
        _unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada");
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa quando a Unidade administradora não tem cidade cadastrada e não persiste nada (CA-02, issue #1114)")]
    public async Task Handle_UnidadeSemCidade_RecusaSemPersistir()
    {
        UnidadeView unidadeSemCidade = new(
            UnidadeId, "CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null);
        _unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(unidadeSemCidade);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.UnidadeAdministradoraSemCidade");
        await _tipoProcessoReader.DidNotReceive().ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa tipo inexistente ou inativo sem criar vínculo")]
    public async Task Handle_TipoInativo_RecusaSemPersistir()
    {
        _tipoProcessoReader.ObterAtivoPorIdAsync(TiposDeProcessoDeTeste.SiSU.OrigemId, Arg.Any<CancellationToken>()).Returns((TipoProcessoView?)null);

        Result<Guid> result = await CriarAsync(
            new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir() with { Nome = "PS" });

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo");
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
    }
    /// <summary>
    /// A recusa por localidade ausente é o código que o catálogo público publica para a
    /// causa — não um erro genérico de validação de contrato. Se virar
    /// <c>uniplus.validacao</c>, o consumidor perde a página que explica o que fazer.
    /// </summary>
    [Fact(DisplayName = "Criação sem localidade é recusada com o erro nomeado, e nada é persistido")]
    public async Task Handle_SemLocalidade_RecusaComErroNomeado()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).ComLocalidade(null, null, null).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.LocalidadeAusente");
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A Unidade administradora tem cidade cadastrada, e mesmo assim a criação é recusada:
    /// é a prova de que o servidor não completa a localidade a partir dela. Deduzir
    /// devolveria a rigidez que tornou a localidade configurável por certame.
    /// </summary>
    [Fact(DisplayName = "Unidade com cidade cadastrada não supre a localidade ausente")]
    public async Task Handle_SemLocalidadeComUnidadeQueTemCidade_NaoDeduz()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).ComLocalidade(null, null, null).Construir();

        Result<Guid> result = await CriarAsync(command);

        UnidadeCeps.CidadeCodigoIbge.Should().Be("1504208", "a unidade tem cidade — e ainda assim não supre");
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.LocalidadeAusente");
    }

    /// <summary>
    /// Declarada uma localidade diferente da cidade da Unidade administradora, é a
    /// declarada que persiste: o certame pode correr sob o calendário de outro município.
    /// </summary>
    [Fact(DisplayName = "Localidade declarada diferente da cidade da Unidade é a que persiste")]
    public async Task Handle_LocalidadeDiferenteDaUnidade_PersisteADeclarada()
    {
        // Belém, não Marabá: a unidade fica numa cidade, o certame corre sob o calendário de outra.
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).ComLocalidade("1501402", "Belém", "PA").Construir();

        Result<Guid> result = await CriarAsync(command);

        result.IsSuccess.Should().BeTrue();
        _persistido!.Localidade.CodigoIbge.Should().Be("1501402");
        _persistido.UnidadeAdministradora.CidadeCodigoIbge.Should().Be("1504208", "o snapshot da unidade guarda a cidade dela, independente da localidade regente");
    }

    [Fact(DisplayName = "Trio de localidade incoerente é recusado pela causa que o Kernel nomeia")]
    public async Task Handle_LocalidadeIncoerente_RecusaPelaCausaDoKernel()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).ComLocalidade("1504208", "Marabá", "SP").Construir();

        Result<Guid> result = await CriarAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CidadeReferencia.UfIncoerente");
    }

    [Fact(DisplayName = "O identificador legível informado na criação é persistido com o processo")]
    public async Task Handle_ComIdentificador_Persiste()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        _persistido!.IdentificadorLegivel!.Value.Valor.Should().Be("psiq-2026");
    }

    [Theory(DisplayName = "Criação sem identificador legível é recusada com o erro nomeado, antes de qualquer leitura")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_SemIdentificador_RecusaComErroNomeadoSemIO(string? identificador)
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).ComIdentificadorLegivel(identificador).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
        await _unidadeReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await _repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "Identificador em formato inválido é recusado antes de qualquer leitura")]
    public async Task Handle_IdentificadorInvalido_RecusaSemIO()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).ComIdentificadorLegivel("PSIQ-2026").Construir();

        Result<Guid> result = await CriarAsync(command);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido);
        await _unidadeReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await _repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "Identificador já usado por outro processo é recusado e nada é persistido")]
    public async Task Handle_IdentificadorEmUso_RecusaSemPersistir()
    {
        _repository.IdentificadorLegivelEmUsoAsync(
                IdentificadorLegivel.Criar("psiq-2026").Value, null, Arg.Any<CancellationToken>())
            .Returns(true);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso);
        await _repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Handle recusa quem não pode configurar na unidade nomeada antes de ler a unidade ou persistir")]
    public async Task Handle_SemPermissaoNaUnidade_RecusaAntesDeQualquerLeitura()
    {
        _verificador.ExigirAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Negado);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.SemPermissaoNaUnidade);
        await _verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == "selecao:processos:configurar"),
            Arg.Is<ResourceContext?>(c => c!.UnidadeProprietariaId == UnidadeId),
            Arg.Any<CancellationToken>());
        await _unidadeReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await _repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "Handle responde identidade incompleta, e não negativa, quando o token não identifica quem cria")]
    public async Task Handle_IdentidadeIncompleta_RecusaComOCodigoProprio()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        _verificador.ExigirAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.IdentidadeIncompleta);
        Result<Guid> result = await CriarAsync(command);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentidadeIncompleta);
    }
}
