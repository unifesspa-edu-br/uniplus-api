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

public sealed class DefinirDistribuicaoVagasRecusasTests
{
    [Fact(DisplayName = "Handle com regra de distribuição inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(NovaOferta(Guid.CreateVersion7()));
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoSemQuadro(Guid.CreateVersion7(), "INEXISTENTE", [Guid.CreateVersion7()])));

        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de regra_distribuicao_vagas recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(NovaOferta(Guid.CreateVersion7()));
        RegraCatalogo regraErrada = RegraCatalogo.Criar("FORMULA_MEDIA_PONDERADA", "v1", TipoRegra.RegraCalculo, Json("{}"), Json("[]"), "base");
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(regraErrada);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoSemQuadro(Guid.CreateVersion7(), "FORMULA_MEDIA_PONDERADA", [Guid.CreateVersion7()])));

        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido");
    }

    [Fact(DisplayName = "Handle com oferta de curso inexistente recusa")]
    public async Task Handle_OfertaCursoNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((OfertaCursoView?)null);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoSemQuadro(Guid.CreateVersion7(), "X", [Guid.CreateVersion7()])));

        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com modalidade inexistente recusa")]
    public async Task Handle_ModalidadeNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid ofertaCursoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo, processo.Id).ComOferta(ofertaCursoId).ComRegraInstitucional();
        mocks.ModalidadeReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ModalidadeView?)null);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoSemQuadro(ofertaCursoId, RegraDeDistribuicaoDeVagas.Institucional.Codigo(), [Guid.CreateVersion7()])));

        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com referência demográfica inexistente recusa")]
    public async Task Handle_ReferenciaDemograficaNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso(TiposDeProcessoDeTeste.SiSU);
        Guid ofertaCursoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo, processo.Id).ComOferta(ofertaCursoId);
        mocks.RegraCatalogoReader.ObterAsync(RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDeDistribuicaoDeVagas.Lei12711.Codigo()));
        mocks.ReferenciaReservaDemograficaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ReferenciaReservaDemograficaView?)null);
        ConfiguracaoDistribuicaoVagasInput distribuicao = DistribuicaoSemQuadro(
            ofertaCursoId, RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), [Guid.CreateVersion7()], 0.5m, Guid.CreateVersion7());

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, distribuicao));

        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaNaoEncontrada");
    }

    /// <summary>Dois ids diferentes resolvendo para o mesmo código: a duplicidade que a configuração rejeita só é visível pelo código, não pelo id.</summary>
    [Fact(DisplayName = "Handle com modalidade duplicada traduz o field do domínio (modalidades) para o do payload (modalidadeIds)")]
    public async Task Handle_ModalidadeDuplicada_TraduzFieldDoDominioParaODoPayload()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid[] modalidadeIds = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        Mocks mocks = NovosMocks(processo, processo.Id).ComOferta(ofertaCursoId).ComRegraInstitucional();
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeIds[0], Arg.Any<CancellationToken>()).Returns(NovaModalidadeCotaReservada(modalidadeIds[0], "IND"));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeIds[1], Arg.Any<CancellationToken>()).Returns(NovaModalidadeCotaReservada(modalidadeIds[1], "IND"));

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, DistribuicaoSemQuadro(ofertaCursoId, RegraDeDistribuicaoDeVagas.Institucional.Codigo(), modalidadeIds)));

        result.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada");
        result.Errors.Select(e => e.Field).Should().OnlyContain(campo => campo == "distribuicaoVagas[0].modalidadeIds");
    }
}
