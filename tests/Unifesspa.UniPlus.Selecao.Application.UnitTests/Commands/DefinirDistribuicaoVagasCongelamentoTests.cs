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

public sealed class DefinirDistribuicaoVagasCongelamentoTests
{
    [Fact(DisplayName = "Handle congela o nome da oferta que o cadastro entrega: curso, grau, unidade ofertante e turnos")]
    public async Task Handle_CongelaONomeDaOferta()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));

        Result<MutacaoComDistribuicaoVagasDto> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            new DefinirDistribuicaoVagasCommand(
                processo.Id,
                [new ConfiguracaoDistribuicaoVagasInput(
                    ofertaCursoId, 60, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                    [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 60)])], PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        OfertaCursoSnapshot congelado = processo.DistribuicaoVagas.Single().OfertaCurso!;
        (congelado.Codigo, congelado.CursoNome, congelado.CursoGrau, congelado.UnidadeOfertanteNome).Should()
            .Be(("MEDICINA_MARABA", "Medicina", "Bacharelado", "Centro de Tecnologia"));
        congelado.Turnos.Should().Equal("MATUTINO");
    }

    [Theory(DisplayName = "Handle congela código e rótulo do grupo de área do curso da oferta, inclusive a ausência dele")]
    [InlineData("HUMANISTICA_I", "Humanística I")]
    [InlineData(null, null)]
    public async Task Handle_CongelaGrupoAreaEnemDaOferta(string? codigo, string? rotulo)
    {
        GrupoAreaEnemView? grupoAreaEnem = codigo is null ? null : new GrupoAreaEnemView(codigo, rotulo!);
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId, grupoAreaEnem));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 60, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 60)])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoComDistribuicaoVagasDto> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        GrupoAreaEnemSnapshot? congelado = processo.DistribuicaoVagas.Single().GrupoAreaEnem;
        GrupoAreaEnemSnapshotDto? devolvido = result.Value!.DistribuicaoVagas.Single().GrupoAreaEnem;
        if (codigo is null)
        {
            congelado.Should().BeNull();
            devolvido.Should().BeNull();
        }
        else
        {
            (congelado!.Codigo, congelado.Rotulo).Should().Be((codigo, rotulo!));
            devolvido.Should().Be(new GrupoAreaEnemSnapshotDto(codigo, rotulo!));
        }
    }

    [Fact(DisplayName = "Grupo da oferta que não forma um grupo válido é recusado no campo grupoAreaEnem da distribuição, acumulado com as demais checagens")]
    public async Task Handle_GrupoDaOfertaInvalido_RecusaNoCampoAcumulado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>())
            .Returns(NovaOferta(ofertaCursoId, new GrupoAreaEnemView(new string('X', 31), "Tecnológica")));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 101, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 101)])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoComDistribuicaoVagasDto> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == "distribuicaoVagas[0].grupoAreaEnem"
            && e.Error.Code == "GrupoAreaEnemSnapshot.TamanhoInvalido");
        result.Errors.Should().Contain(e => e.Field == "distribuicaoVagas[0].vagasOfertadasBase"
            && e.Error.Code == "ConfiguracaoDistribuicaoVagas.VagasOfertadasBaseAcimaDasVagasAutorizadas",
            "o grupo inválido não interrompe as demais checagens da distribuição (ADR-0125): a oferta autoriza 100 vagas");
    }

    [Fact(DisplayName = "Grupo alterado no cadastro depois da definição não alcança o processo")]
    public async Task Handle_CadastroAlteradoDepois_ProcessoMantemGrupoCongelado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId, new GrupoAreaEnemView("HUMANISTICA_I", "Humanística I")));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 60, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 60)])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoComDistribuicaoVagasDto> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);

        // O administrador troca o grupo do curso no cadastro.
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId, new GrupoAreaEnemView("TECNOLOGICA", "Tecnológica")));

        // A leitura do processo passa pela mesma rota do GET, que não recebe o reader de
        // ofertas: o que ela devolve só pode vir do que foi congelado na definição.
        mocks.Repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ProcessoSeletivoDto? lido = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), mocks.Repository, CancellationToken.None);

        lido!.DistribuicaoVagas.Single().GrupoAreaEnem.Should().Be(new GrupoAreaEnemSnapshotDto("HUMANISTICA_I", "Humanística I"),
            "o grupo é copiado por valor na definição; o processo não relê o cadastro");
        await mocks.OfertaCursoReader.Received(1).ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>());
    }
}
