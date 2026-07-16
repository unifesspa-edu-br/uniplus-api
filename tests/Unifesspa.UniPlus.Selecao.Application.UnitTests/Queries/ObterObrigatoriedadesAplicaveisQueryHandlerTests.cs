namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ObterObrigatoriedadesAplicaveisQueryHandlerTests
{
    [Fact(DisplayName = "Handle delega ao ruleset com o tipo do processo e a data explícita")]
    public async Task Handle_DataExplicita_DelegadaAoRepositorio()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        DateOnly dataReferencia = new(2026, 6, 15);
        ObrigatoriedadeLegal universal = NovaRegra("*", "UNIVERSAL");
        ObrigatoriedadeLegal especifica = NovaRegra("PSIQ", "PSIQ");

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterPorIdAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IObrigatoriedadeLegalRepository regraRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        regraRepository.ObterVigentesParaTipoProcessoAsync("PSIQ", dataReferencia, Arg.Any<CancellationToken>())
            .Returns([universal, especifica]);

        IReadOnlyList<ObrigatoriedadeLegalDto> resultado = await ObterObrigatoriedadesAplicaveisQueryHandler.Handle(
            new ObterObrigatoriedadesAplicaveisQuery(processo.Id, dataReferencia),
            processoRepository,
            regraRepository,
            CancellationToken.None);

        resultado.Select(static regra => regra.RegraCodigo).Should().BeEquivalentTo(["UNIVERSAL", "PSIQ"]);
        await regraRepository.Received(1).ObterVigentesParaTipoProcessoAsync(
            "PSIQ", dataReferencia, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle não consulta ruleset quando o processo não existe")]
    public async Task Handle_ProcessoInexistente_RetornaVazio()
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        IObrigatoriedadeLegalRepository regraRepository = Substitute.For<IObrigatoriedadeLegalRepository>();

        IReadOnlyList<ObrigatoriedadeLegalDto> resultado = await ObterObrigatoriedadesAplicaveisQueryHandler.Handle(
            new ObterObrigatoriedadesAplicaveisQuery(Guid.CreateVersion7(), new DateOnly(2026, 6, 15)),
            processoRepository,
            regraRepository,
            CancellationToken.None);

        resultado.Should().BeEmpty();
        await regraRepository.DidNotReceiveWithAnyArgs()
            .ObterVigentesParaTipoProcessoAsync(default!, default, default);
    }

    [Fact(DisplayName = "Handle devolve conjuntos diferentes para datas de referência diferentes")]
    public async Task Handle_DatasDiferentes_DevolveConjuntosDiferentes()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        DateOnly duranteVigencia = new(2026, 6, 15);
        DateOnly aposVigencia = new(2026, 7, 5);
        ObrigatoriedadeLegal regra = NovaRegra("PSIQ", "PSIQ_VIGENTE");

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterPorIdAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IObrigatoriedadeLegalRepository regraRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        regraRepository.ObterVigentesParaTipoProcessoAsync("PSIQ", duranteVigencia, Arg.Any<CancellationToken>())
            .Returns([regra]);
        regraRepository.ObterVigentesParaTipoProcessoAsync("PSIQ", aposVigencia, Arg.Any<CancellationToken>())
            .Returns([]);

        IReadOnlyList<ObrigatoriedadeLegalDto> durante = await ObterObrigatoriedadesAplicaveisQueryHandler.Handle(
            new ObterObrigatoriedadesAplicaveisQuery(processo.Id, duranteVigencia),
            processoRepository,
            regraRepository,
            CancellationToken.None);
        IReadOnlyList<ObrigatoriedadeLegalDto> apos = await ObterObrigatoriedadesAplicaveisQueryHandler.Handle(
            new ObterObrigatoriedadesAplicaveisQuery(processo.Id, aposVigencia),
            processoRepository,
            regraRepository,
            CancellationToken.None);

        durante.Select(static item => item.RegraCodigo).Should().ContainSingle().Which.Should().Be("PSIQ_VIGENTE");
        apos.Should().BeEmpty();
    }

    private static ObrigatoriedadeLegal NovaRegra(string tipoProcessoCodigo, string regraCodigo) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo,
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            new ConcorrenciaDuplaObrigatoria(),
            "Descrição",
            "Lei",
            new DateOnly(2026, 1, 1)).Value!;
}
