namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de <see cref="ObterProcessoSeletivoQueryHandler"/> para o bloco de
/// classificação — em particular <see cref="ConfiguracaoClassificacao.BaseadoEmEnem"/>
/// (issue #850), que a projeção precisa expor: sem este teste, trocar a leitura por um
/// <see langword="false"/> hard-coded em <c>ProjectClassificacao</c> compilaria e nenhum
/// teste existente acusaria.
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerClassificacaoTests
{
    [Fact(DisplayName = "Handle projeta BaseadoEmEnem=true da classificação para o DTO de leitura")]
    public async Task Handle_ProjetaBaseadoEmEnemTrue()
    {
        ConfiguracaoClassificacaoDto? dto = await ObterDtoComClassificacaoAsync(baseadoEmEnem: true);

        dto.Should().NotBeNull();
        dto!.BaseadoEmEnem.Should().BeTrue();
    }

    [Fact(DisplayName = "Handle projeta a resolução de Pesos por Área e o quadro congelado, ordenados pelo código do grupo e da área")]
    public async Task Handle_ProjetaResolucaoEQuadroCongelado()
    {
        ConfiguracaoClassificacaoDto? dto = await ObterDtoComClassificacaoAsync(baseadoEmEnem: true);

        dto!.ResolucaoPesoAreaEnem.Should().Be(QuadroPesoAreaEnemDeTeste.Resolucao);
        dto.QuadroPesoAreaEnem.Select(g => g.GrupoAreaEnem.Codigo).Should().Equal(
            "HUMANISTICA_I", "HUMANISTICA_II", "SAUDE_E_BIOLOGICAS", "TECNOLOGICA");

        GrupoPesoAreaEnemCongeladoDto saude = dto.QuadroPesoAreaEnem[2];
        saude.GrupoAreaEnem.Rotulo.Should().Be("Saúde e Biológicas");
        saude.BaseLegal.Should().Be("Resolução nº 805/2024/Consepe – Anexo I");
        saude.Areas.Should().Equal(
            new AreaPesoAreaEnemCongeladaDto("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, null),
            new AreaPesoAreaEnemCongeladaDto("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, null),
            new AreaPesoAreaEnemCongeladaDto("LINGUAGENS", "Linguagens e suas Tecnologias", 1.50m, null),
            new AreaPesoAreaEnemCongeladaDto("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, null),
            new AreaPesoAreaEnemCongeladaDto("REDACAO", "Redação", 2.00m, 400m));
    }

    [Fact(DisplayName = "Handle projeta classificação sem resolução com quadro vazio")]
    public async Task Handle_SemResolucao_ProjetaQuadroVazio()
    {
        ConfiguracaoClassificacaoDto? dto = await ObterDtoComClassificacaoAsync(baseadoEmEnem: false);

        dto!.ResolucaoPesoAreaEnem.Should().BeNull();
        dto.QuadroPesoAreaEnem.Should().BeEmpty();
    }

    [Fact(DisplayName = "Handle projeta BaseadoEmEnem=false da classificação para o DTO de leitura")]
    public async Task Handle_ProjetaBaseadoEmEnemFalse()
    {
        ConfiguracaoClassificacaoDto? dto = await ObterDtoComClassificacaoAsync(baseadoEmEnem: false);

        dto.Should().NotBeNull();
        dto!.BaseadoEmEnem.Should().BeFalse();
    }

    private static async Task<ConfiguracaoClassificacaoDto?> ObterDtoComClassificacaoAsync(bool baseadoEmEnem)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Query Classificação", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        ReferenciaRegra regraCalculo = ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)).Value!;
        ReferenciaRegra regraArredondamento = ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('b', 64)).Value!;
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('c', 64)).Value!;

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo, regraArredondamento, casasArredondamento: 2, regraOrdemAlocacao, nOpcoesAlocacao: 1, [],
            baseadoEmEnem,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : []).Value!;

        Result resultado = processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente);
        resultado.IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        return dto?.Classificacao;
    }
}
