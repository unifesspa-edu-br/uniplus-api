namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaDistribuicaoDeVagas;

public sealed class DefinirDistribuicaoVagasRecusasTests
{
    [Fact(DisplayName = "Handle com regra de distribuição inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(NovaOferta(Guid.CreateVersion7()));
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "INEXISTENTE", "v1", null, null, null, [Guid.CreateVersion7()], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de regra_distribuicao_vagas recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(NovaOferta(Guid.CreateVersion7()));
        RegraCatalogo regraErrada = RegraCatalogo.Criar(
            "FORMULA-MEDIA-PONDERADA", "v1", TipoRegra.RegraCalculo, Json("{}"), Json("[]"), "base");
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(regraErrada);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "FORMULA-MEDIA-PONDERADA", "v1", null, null, null, [Guid.CreateVersion7()], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido");
    }

    [Fact(DisplayName = "Handle com oferta de curso inexistente recusa")]
    public async Task Handle_OfertaCursoNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((OfertaCursoView?)null);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "X", "v1", null, null, null, [Guid.CreateVersion7()], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com modalidade inexistente recusa")]
    public async Task Handle_ModalidadeNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ModalidadeView?)null);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 50, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null, [Guid.CreateVersion7()], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com referência demográfica inexistente recusa")]
    public async Task Handle_ReferenciaDemograficaNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("SiSU 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Lei12711, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Lei12711));
        mocks.ReferenciaReservaDemograficaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ReferenciaReservaDemograficaView?)null);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 50, 0.5m, RegraDistribuicaoVagasCodigo.Lei12711, "v1", null, null, Guid.CreateVersion7(), [Guid.CreateVersion7()], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com modalidade duplicada traduz o field do domínio (modalidades) para o do payload (modalidadeIds)")]
    public async Task Handle_ModalidadeDuplicada_TraduzFieldDoDominioParaODoPayload()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId1 = Guid.CreateVersion7();
        Guid modalidadeId2 = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        // Dois Ids diferentes resolvendo para o MESMO código — a duplicidade que
        // ConfiguracaoDistribuicaoVagas.Criar rejeita só é visível pelo Codigo, não pelo Id.
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId1, Arg.Any<CancellationToken>()).Returns(NovaModalidadeCotaReservada(modalidadeId1, "IND"));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId2, Arg.Any<CancellationToken>()).Returns(NovaModalidadeCotaReservada(modalidadeId2, "IND"));

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 60, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                [modalidadeId1, modalidadeId2], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada");
        result.Errors.Select(e => e.Field).Should().OnlyContain(campo => campo == "distribuicaoVagas[0].modalidadeIds");
    }
}
