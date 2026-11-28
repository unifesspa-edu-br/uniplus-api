namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

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
    private static Guid UnidadeId => CenarioDaCriacaoDoProcesso.UnidadeId;

    private readonly CenarioDaCriacaoDoProcesso _cenario = new();

    private Task<Result<Guid>> CriarAsync(CriarProcessoSeletivoCommand comando) => _cenario.CriarAsync(comando);

    [Fact]
    public async Task Handle_ComDadosValidos_PersisteRascunhoEDevolveIdentificador()
    {
        Result<Guid> resultado = await CriarAsync(new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir());

        resultado.IsSuccess.Should().BeTrue();
        _cenario.Persistido.Should().NotBeNull();
        resultado.Value.Should().Be(_cenario.Persistido!.Id);
        _cenario.Persistido.Nome.Should().Be("PS 2026 — SiSU");
        _cenario.Persistido.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Rascunho);
        await _cenario.Repository.Received(1).AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ComDadosValidos_CopiaUnidadeAdministradora()
    {
        await CriarAsync(new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir());

        _cenario.Persistido!.UnidadeAdministradora.Should().BeEquivalentTo(new
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
        _cenario.Persistido.UnidadeAdministradoraOrigemId.Should().Be(UnidadeId);
    }

    [Fact]
    public async Task Handle_ComDadosValidos_VinculaTipoDoProcesso()
    {
        await CriarAsync(new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir());

        _cenario.Persistido!.TipoProcessoOrigemId.Should().Be(CenarioDaCriacaoDoProcesso.TipoSisu.Id);
        _cenario.Persistido.TipoProcesso.Codigo.Should().Be("SISU");
        _cenario.Persistido.TipoProcesso.Nome.Should().Be("SiSU");
    }

    [Fact(DisplayName = "Handle recusa quando a Unidade administradora não é encontrada e não persiste nada (CA-02)")]
    public async Task Handle_UnidadeNaoEncontrada_RecusaSemPersistir()
    {
        _cenario.UnidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada");
        result.Error.Message.Should().Be("A unidade administradora deve existir no cadastro de unidades.");
        result.Errors.Should().ContainSingle().Which.Field.Should().Be("unidadeAdministradoraOrigemId");
        await _cenario.Repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa quando a Unidade administradora não tem cidade cadastrada e não persiste nada (CA-02, issue #1114)")]
    public async Task Handle_UnidadeSemCidade_RecusaSemPersistir()
    {
        UnidadeView unidadeSemCidade = new(
            UnidadeId, "CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null);
        _cenario.UnidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(unidadeSemCidade);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.UnidadeAdministradoraSemCidade");
        result.Errors.Should().ContainSingle().Which.Field.Should().Be("unidadeAdministradoraOrigemId");
        result.Error.Message.Should().NotContain(UnidadeId.ToString());
        await _cenario.Repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa tipo inexistente ou inativo sem criar vínculo")]
    public async Task Handle_TipoInativo_RecusaSemPersistir()
    {
        _cenario.TipoProcessoReader.ObterAtivoPorIdAsync(TiposDeProcessoDeTeste.Sisu.OrigemId, Arg.Any<CancellationToken>()).Returns((TipoProcessoView?)null);

        Result<Guid> result = await CriarAsync(
            new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir() with { Nome = "PS" });

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo");
        result.Error.Message.Should().Be("O tipo de processo seletivo deve existir no cadastro de tipos de processo e estar ativo.");
        result.Errors.Should().ContainSingle().Which.Field.Should().Be("tipoProcessoOrigemId");
        await _cenario.Repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
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
        await _cenario.Repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
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

        CenarioDaCriacaoDoProcesso.UnidadeCeps.CidadeCodigoIbge.Should().Be("1504208", "a unidade tem cidade — e ainda assim não supre");
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
        _cenario.Persistido!.Localidade.CodigoIbge.Should().Be("1501402");
        _cenario.Persistido.UnidadeAdministradora.CidadeCodigoIbge.Should().Be("1504208", "o snapshot da unidade guarda a cidade dela, independente da localidade regente");
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
        _cenario.Persistido!.IdentificadorLegivel!.Value.Valor.Should().Be("psiq-2026");
    }

    [Theory(DisplayName = "Criação sem identificador legível é recusada com o erro nomeado, no campo do identificador, e nada é persistido")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_SemIdentificador_RecusaComErroNomeado(string? identificador)
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).ComIdentificadorLegivel(identificador).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(recusa =>
            recusa.Field == "identificadorLegivel" && recusa.Error.Code == ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Identificador em formato inválido é recusado no campo do identificador, sem consultar o uso e sem persistir")]
    public async Task Handle_IdentificadorInvalido_RecusaSemConsultarOUso()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).ComIdentificadorLegivel("PSIQ-2026").Construir();

        Result<Guid> result = await CriarAsync(command);

        result.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(recusa =>
            recusa.Field == "identificadorLegivel" && recusa.Error.Code == ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido);
        await _cenario.Repository.DidNotReceiveWithAnyArgs().IdentificadorLegivelEmUsoAsync(default, default, default);
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Identificador já usado por outro processo é recusado e nada é persistido")]
    public async Task Handle_IdentificadorEmUso_RecusaSemPersistir()
    {
        _cenario.Repository.IdentificadorLegivelEmUsoAsync(
                IdentificadorLegivel.Criar("psiq-2026").Value, null, Arg.Any<CancellationToken>())
            .Returns(true);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso);
        await _cenario.Repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        await _cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Handle recusa quem não pode configurar na unidade nomeada antes de ler a unidade ou persistir")]
    public async Task Handle_SemPermissaoNaUnidade_RecusaAntesDeQualquerLeitura()
    {
        _cenario.Verificador.ExigirAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Negado);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        Result<Guid> result = await CriarAsync(command);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.SemPermissaoNaUnidade);
        await _cenario.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == "selecao:processos:configurar"),
            Arg.Is<ResourceContext?>(c => c!.UnidadeProprietariaId == UnidadeId),
            Arg.Any<CancellationToken>());
        await _cenario.UnidadeReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await _cenario.Repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "Sem permissão na unidade, dados inválidos são recusados por falta de permissão, sem apontar campo nem ler nada")]
    public async Task Handle_SemPermissaoEDadosInvalidos_RecusaSoPorFaltaDePermissao()
    {
        _cenario.ConcederAcesso(ResultadoDoAcesso.Negado);
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir() with { Nome = string.Empty, OrigemCandidatos = "origemQueNaoExiste" };

        Result<Guid> result = await CriarAsync(command);

        result.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(recusa =>
            recusa.Field == null && recusa.Error.Code == ProcessoSeletivoErrorCodes.SemPermissaoNaUnidade);
        await _cenario.NadaFoiLidoNemGravadoAsync();
    }

    [Fact(DisplayName = "Com identidade incompleta, dados inválidos também recebem a recusa de identidade, e não a dos dados")]
    public async Task Handle_IdentidadeIncompletaEDadosInvalidos_RecusaAIdentidade()
    {
        _cenario.ConcederAcesso(ResultadoDoAcesso.IdentidadeIncompleta);

        Result<Guid> result = await CriarAsync(new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir() with { Nome = string.Empty });

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentidadeIncompleta);
    }

    [Fact(DisplayName = "Com permissão na unidade, os dados inválidos saem juntos, cada um no seu campo, na ordem nome, origem, tipo, sem persistir")]
    public async Task Handle_ComPermissaoEDadosInvalidos_RecusaTodasAsCausasJuntas()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir() with
        {
            Nome = string.Empty,
            TipoProcessoOrigemId = Guid.Empty,
            OrigemCandidatos = null,
        };

        Result<Guid> result = await CriarAsync(command);

        result.Errors.Select(static erro => erro.Field).Should().Equal("nome", "origemCandidatos", "tipoProcessoOrigemId");
        await _cenario.TipoProcessoReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Sem unidade informada não há permissão a decidir: a falta da unidade sai com as demais causas")]
    public async Task Handle_SemUnidade_NaoConsultaAPermissaoERecusaOsDados()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir() with
        {
            UnidadeAdministradoraOrigemId = Guid.Empty,
            Nome = string.Empty,
        };

        Result<Guid> result = await CriarAsync(command);

        result.Errors.Select(static erro => erro.Field).Should().Equal("nome", "unidadeAdministradoraOrigemId");
        await _cenario.Verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
        await _cenario.NadaFoiLidoNemGravadoAsync();
    }

    [Fact(DisplayName = "Handle responde identidade incompleta, e não negativa, quando o token não identifica quem cria")]
    public async Task Handle_IdentidadeIncompleta_RecusaComOCodigoProprio()
    {
        CriarProcessoSeletivoCommand command = new CriacaoDoProcessoSeletivoBuilder(UnidadeId).Construir();

        _cenario.Verificador.ExigirAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.IdentidadeIncompleta);
        Result<Guid> result = await CriarAsync(command);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentidadeIncompleta);
    }
}
