namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

public sealed class DefinirDistribuicaoVagasCommandHandlerTests
{
    [Fact(DisplayName = "Handle com lista vazia devolve a causa de domínio, não a recusa de forma")]
    public async Task Handle_ListaVazia_DevolveCausaDeDominio()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirDistribuicaoVagasCommand command = new(processo.Id, []);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DistribuicaoVagasVazia",
            "a regra de forma no validator tornava esta causa inalcançável por um cliente HTTP");
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirDistribuicaoVagasCommand command = new(
            Guid.CreateVersion7(),
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "X", "v1", null, null, null, [Guid.CreateVersion7()], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle institucional (sem referência demográfica) persiste")]
    public async Task Handle_Institucional_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo, processo.Id);
        PrepararInstitucional(mocks, ofertaCursoId, modalidadeId);
        DefinirDistribuicaoVagasCommand command = ComandoInstitucional(processo.Id, ofertaCursoId, modalidadeId);
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        ConferirVagasInstitucionais(processo);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        ConferirDistribuicaoInstitucional(result);
    }

    [Fact(DisplayName = "Handle Lei 12.711 resolve a referência demográfica e persiste")]
    public async Task Handle_Lei12711_ResolveReferenciaDemografica_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("SiSU 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid referenciaId = Guid.CreateVersion7();
        (string Codigo, Guid Id)[] federaisMaisAc = ModalidadesFederais();
        Mocks mocks = NovosMocks(processo, processo.Id);
        PrepararReservaLegal(mocks, ofertaCursoId, referenciaId, federaisMaisAc);
        DefinirDistribuicaoVagasCommand command = ComandoReservaLegal(processo.Id, ofertaCursoId, referenciaId, federaisMaisAc);
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.Resolvedor, CadastrosVivos.CatalogoDeFatos(), mocks.UnitOfWork, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        ConferirDistribuicaoFederal(processo);
        ConferirReservaLegal(result, processo);
    }

    private static void ConferirDistribuicaoInstitucional(Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result)
    {
        // O corpo devolvido reflete exatamente o que foi persistido, no mesmo formato da simulação e do GET.
        ConfiguracaoDistribuicaoVagasDto dto = result.Value!.Should().ContainSingle().Subject;
        dto.VagasOfertadasBase.Should().Be(60);
        dto.Quadro.Should().ContainSingle(v => v.Quantidade == 60);
        dto.TotalDeVagasDoEdital.Should().Be(60);
    }

    private static void ConferirReservaLegal(Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result, ProcessoSeletivo processo)
    {
        // A conservação do quadro (VrFinal + retiradas + AC = VO) confere no DTO devolvido, não só no agregado em memória.
        ConfiguracaoDistribuicaoVagasDto dto = result.Value!.Should().ContainSingle().Subject;
        dto.Quadro.Should().HaveCount(9);
        dto.ReferenciaDemografica.Should().NotBeNull();
        dto.ConferenciaDaReservaLegal!.ReservaLegalEfetiva.Should().Be(processo.DistribuicaoVagas.Single().ReservaLegalEfetiva!.Value);
        dto.TotalDeVagasDoEdital.Should().Be(processo.DistribuicaoVagas.Single().TotalDeVagasDoEdital);
        int ac = dto.Quadro.Single(v => v.ModalidadeCodigo == ModalidadesFederaisLei12711.Ac).Quantidade;
        (dto.ConferenciaDaReservaLegal.ReservaLegalEfetiva + ac).Should().Be(50);
    }

    private static void PrepararReservaLegal(Mocks mocks, Guid ofertaCursoId, Guid referenciaId, (string Codigo, Guid Id)[] federaisMaisAc)
    {
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDeDistribuicaoDeVagas.Lei12711.Codigo()));
        mocks.RegraCatalogoReader.ObterAsync("AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                "AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", "v1", TipoRegra.RegraAjusteDistribuicaoVagas, Json("{}"), Json("[]"), "art. 11 §único"));
        mocks.ReferenciaReservaDemograficaReader.ObterPorIdAsync(referenciaId, Arg.Any<CancellationToken>())
            .Returns(new ReferenciaReservaDemograficaView(referenciaId, "2022", 79m, 1.5m, 8.5m, "Censo 2022"));
        PrepararModalidadesFederais(mocks, federaisMaisAc);
    }

    private static void PrepararModalidadesFederais(Mocks mocks, (string Codigo, Guid Id)[] federaisMaisAc)
    {
        foreach ((string codigo, Guid id) in federaisMaisAc)
        {
            ModalidadeView view = codigo == ModalidadesFederaisLei12711.Ac
                ? NovaModalidadeAmpla(id)
                : NovaModalidadeCotaReservada(id, codigo);
            mocks.ModalidadeReader.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns(view);
        }

    }

    private static void PrepararInstitucional(Mocks mocks, Guid ofertaCursoId, Guid modalidadeId)
    {
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDeDistribuicaoDeVagas.Institucional.Codigo()));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));
    }

    private static DefinirDistribuicaoVagasCommand ComandoInstitucional(Guid processoId, Guid ofertaCursoId, Guid modalidadeId)
    {
        return new DefinirDistribuicaoVagasCommand(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 60, 1m, RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", null, null, null,
                [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 60)])]);
    }

    private static void ConferirVagasInstitucionais(ProcessoSeletivo processo)
    {
        processo.DistribuicaoVagas.Should().ContainSingle();
        processo.DistribuicaoVagas.Single().VagasOfertadasBase.Should().Be(60);
        processo.DistribuicaoVagas.Single().VagasOfertadas.Should().ContainSingle(v => v.Quantidade == 60);
    }

    private static (string Codigo, Guid Id)[] ModalidadesFederais()
    {
        // A Lei 12.711 exige as 8 modalidades federais + AC.
        return
        [
            .. ModalidadesFederaisLei12711.Codigos.Select(codigo => (codigo, Guid.CreateVersion7())),
            (ModalidadesFederaisLei12711.Ac, Guid.CreateVersion7()),
        ];
    }

    private static DefinirDistribuicaoVagasCommand ComandoReservaLegal(Guid processoId, Guid ofertaCursoId, Guid referenciaId, (string Codigo, Guid Id)[] federaisMaisAc)
    {
        return new DefinirDistribuicaoVagasCommand(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 50, 0.5m, RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), "v1",
                "AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", "v1", referenciaId,
                [.. federaisMaisAc.Select(f => f.Id)], [])]);
    }

    private static void ConferirDistribuicaoFederal(ProcessoSeletivo processo)
    {
        processo.DistribuicaoVagas.Single().ReferenciaDemografica.Should().NotBeNull();
        processo.DistribuicaoVagas.Single().ReferenciaDemografica!.CensoReferencia.Should().Be("2022");
        processo.DistribuicaoVagas.Single().Modalidades.Should().HaveCount(9);
        processo.DistribuicaoVagas.Single().RegraAjuste.Should().NotBeNull();
        processo.DistribuicaoVagas.Single().VagasOfertadas.Should().HaveCount(9);
    }

}
