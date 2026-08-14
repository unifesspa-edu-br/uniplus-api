namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CriarProcessoSeletivoCommandHandlerTests
{
    private static readonly Guid UnidadeId = Guid.NewGuid();

    private static readonly UnidadeView UnidadeCeps = new(
        UnidadeId, "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null,
        "1504208", "Marabá", "PA");

    private static readonly TipoProcessoView TipoSisu = new(
        TipoProcesso.SiSU.OrigemId, "SiSU", "SiSU", null);

    [Fact(DisplayName = "Handle persiste o processo em rascunho com a Unidade resolvida e retorna o id (CA-01, CA-03)")]
    public async Task Handle_PersisteComUnidadeResolvidaERetornaId()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        tipoProcessoReader.ObterAtivoPorIdAsync(TipoProcesso.SiSU.OrigemId, Arg.Any<CancellationToken>()).Returns(TipoSisu);
        CriarProcessoSeletivoCommand command = new("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "PA");
        ProcessoSeletivo? processoPersistido = null;
        repository.When(r => r.AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>()))
            .Do(ci => processoPersistido = ci.Arg<ProcessoSeletivo>());

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, tipoProcessoReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processoPersistido.Should().NotBeNull();
        result.Value.Should().Be(processoPersistido!.Id, "o id devolvido tem de ser o do processo efetivamente persistido");
        await repository.Received(1).AdicionarAsync(
            Arg.Is<ProcessoSeletivo>(p =>
                p.Nome == "PS 2026 — SiSU"
                && p.Status == StatusProcesso.Rascunho
                && p.UnidadeAdministradoraOrigemId == UnidadeId
                && p.UnidadeAdministradora.Sigla == "CEPS"
                && p.UnidadeAdministradora.Slug == "ceps"
                && p.UnidadeAdministradora.Nome == "Centro de Processos Seletivos"
                && p.UnidadeAdministradora.Tipo == "ADMINISTRATIVA"
                && p.UnidadeAdministradora.CidadeCodigoIbge == "1504208"
                && p.UnidadeAdministradora.CidadeNome == "Marabá"
                && p.UnidadeAdministradora.CidadeUf == "PA"
                && p.TipoProcessoOrigemId == TipoSisu.Id
                && p.TipoProcesso.Codigo == "SiSU"
                && p.TipoProcesso.Nome == "SiSU"),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa quando a Unidade administradora não é encontrada e não persiste nada (CA-02)")]
    public async Task Handle_UnidadeNaoEncontrada_RecusaSemPersistir()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);
        CriarProcessoSeletivoCommand command = new("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "PA");

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, tipoProcessoReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa quando a Unidade administradora não tem cidade cadastrada e não persiste nada (CA-02, issue #1114)")]
    public async Task Handle_UnidadeSemCidade_RecusaSemPersistir()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        UnidadeView unidadeSemCidade = new(
            UnidadeId, "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null);
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(unidadeSemCidade);
        CriarProcessoSeletivoCommand command = new("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "PA");

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, tipoProcessoReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.UnidadeAdministradoraSemCidade");
        await tipoProcessoReader.DidNotReceive().ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa tipo inexistente ou inativo sem criar vínculo")]
    public async Task Handle_TipoInativo_RecusaSemPersistir()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        tipoProcessoReader.ObterAtivoPorIdAsync(TipoProcesso.SiSU.OrigemId, Arg.Any<CancellationToken>()).Returns((TipoProcessoView?)null);

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            new CriarProcessoSeletivoCommand("PS", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "PA"),
            repository, unidadeReader, tipoProcessoReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
    }
    /// <summary>
    /// A recusa por localidade ausente é o código que o catálogo público publica para a
    /// causa — não um erro genérico de validação de contrato. Se virar
    /// <c>uniplus.validacao</c>, o consumidor perde a página que explica o que fazer.
    /// </summary>
    [Fact(DisplayName = "Criação sem localidade é recusada com o erro nomeado, e nada é persistido")]
    public async Task Handle_SemLocalidade_RecusaComErroNomeado()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        tipoProcessoReader.ObterAtivoPorIdAsync(TipoProcesso.SiSU.OrigemId, Arg.Any<CancellationToken>()).Returns(TipoSisu);
        CriarProcessoSeletivoCommand command = new(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, null, null, null);

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, tipoProcessoReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.LocalidadeAusente");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A Unidade administradora tem cidade cadastrada, e mesmo assim a criação é recusada:
    /// é a prova de que o servidor não completa a localidade a partir dela. Deduzir
    /// devolveria a rigidez que tornou a localidade configurável por certame.
    /// </summary>
    [Fact(DisplayName = "Unidade com cidade cadastrada não supre a localidade ausente")]
    public async Task Handle_SemLocalidadeComUnidadeQueTemCidade_NaoDeduz()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        tipoProcessoReader.ObterAtivoPorIdAsync(TipoProcesso.SiSU.OrigemId, Arg.Any<CancellationToken>()).Returns(TipoSisu);
        CriarProcessoSeletivoCommand command = new(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, null, null, null);

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, tipoProcessoReader, unitOfWork, CancellationToken.None);

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
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        tipoProcessoReader.ObterAtivoPorIdAsync(TipoProcesso.SiSU.OrigemId, Arg.Any<CancellationToken>()).Returns(TipoSisu);
        // Belém, não Marabá: a unidade fica numa cidade, o certame corre sob o calendário de outra.
        CriarProcessoSeletivoCommand command = new(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1501402", "Belém", "PA");
        ProcessoSeletivo? persistido = null;
        repository.When(r => r.AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>()))
            .Do(ci => persistido = ci.Arg<ProcessoSeletivo>());

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, tipoProcessoReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persistido!.Localidade.CodigoIbge.Should().Be("1501402");
        persistido.UnidadeAdministradora.CidadeCodigoIbge.Should().Be("1504208", "o snapshot da unidade guarda a cidade dela, independente da localidade regente");
    }

    [Fact(DisplayName = "Trio de localidade incoerente é recusado pela causa que o Kernel nomeia")]
    public async Task Handle_LocalidadeIncoerente_RecusaPelaCausaDoKernel()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        tipoProcessoReader.ObterAtivoPorIdAsync(TipoProcesso.SiSU.OrigemId, Arg.Any<CancellationToken>()).Returns(TipoSisu);
        CriarProcessoSeletivoCommand command = new(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "SP");

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, tipoProcessoReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CidadeReferencia.UfIncoerente");
    }
}
