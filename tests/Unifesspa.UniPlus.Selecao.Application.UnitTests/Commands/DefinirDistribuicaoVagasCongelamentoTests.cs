namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
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
        ProcessoSeletivo processo = NovoProcesso();
        (Guid ofertaCursoId, Guid modalidadeId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        Mocks mocks = NovosMocks(processo, processo.Id).ComOferta(ofertaCursoId).ComRegraInstitucional().ComModalidadeAmpla(modalidadeId);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoInstitucional(ofertaCursoId, modalidadeId, 60)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        CopiaDaOfertaDeCurso congelado = processo.DistribuicaoVagas.Single().OfertaCurso!;
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
        ProcessoSeletivo processo = NovoProcesso();
        (Guid ofertaCursoId, Guid modalidadeId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        Mocks mocks = NovosMocks(processo, processo.Id).ComOferta(ofertaCursoId, grupoAreaEnem).ComRegraInstitucional().ComModalidadeAmpla(modalidadeId);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoInstitucional(ofertaCursoId, modalidadeId, 60)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        ConferirGrupoCongelado(processo.DistribuicaoVagas.Single().GrupoAreaEnem, result.Value!.Single().GrupoAreaEnem, codigo, rotulo);
    }

    [Fact(DisplayName = "Grupo da oferta que não forma um grupo válido é recusado no campo grupoAreaEnem da distribuição, acumulado com as demais checagens")]
    public async Task Handle_GrupoDaOfertaInvalido_RecusaNoCampoAcumulado()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (Guid ofertaCursoId, Guid modalidadeId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        GrupoAreaEnemView grupoLongoDemais = new(new string('X', 31), "Tecnológica");
        Mocks mocks = NovosMocks(processo, processo.Id).ComOferta(ofertaCursoId, grupoLongoDemais).ComRegraInstitucional().ComModalidadeAmpla(modalidadeId);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoInstitucional(ofertaCursoId, modalidadeId, 101)));

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
        ProcessoSeletivo processo = NovoProcesso();
        (Guid ofertaCursoId, Guid modalidadeId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        Mocks mocks = NovosMocks(processo, processo.Id)
            .ComOferta(ofertaCursoId, new GrupoAreaEnemView("HUMANISTICA_I", "Humanística I")).ComRegraInstitucional().ComModalidadeAmpla(modalidadeId);
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoInstitucional(ofertaCursoId, modalidadeId, 60)));
        result.IsSuccess.Should().BeTrue(result.Error?.Message);

        mocks.ComOferta(ofertaCursoId, new GrupoAreaEnemView("TECNOLOGICA", "Tecnológica"));
        ProcessoSeletivoDto? lido = await LerPeloGetAsync(mocks, processo);

        lido!.DistribuicaoVagas.Single().GrupoAreaEnem.Should().Be(new GrupoAreaEnemSnapshotDto("HUMANISTICA_I", "Humanística I"),
            "o grupo é copiado por valor na definição; o processo não relê o cadastro");
        await mocks.OfertaCursoReader.Received(1).ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>());
    }

    private static void ConferirGrupoCongelado(GrupoAreaEnemSnapshot? congelado, GrupoAreaEnemSnapshotDto? devolvido, string? codigo, string? rotulo)
    {
        if (codigo is null)
        {
            congelado.Should().BeNull();
            devolvido.Should().BeNull();
            return;
        }

        (congelado!.Codigo, congelado.Rotulo).Should().Be((codigo, rotulo!));
        devolvido.Should().Be(new GrupoAreaEnemSnapshotDto(codigo, rotulo!));
    }

    /// <summary>A leitura passa pela mesma rota do GET, que não recebe o reader de ofertas: só devolve o que foi congelado na definição.</summary>
    private static Task<ProcessoSeletivoDto?> LerPeloGetAsync(Mocks mocks, ProcessoSeletivo processo)
    {
        mocks.Repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return ObterProcessoSeletivoQueryHandler.Handle(new ObterProcessoSeletivoQuery(processo.Id), mocks.Repository, CancellationToken.None);
    }
}
