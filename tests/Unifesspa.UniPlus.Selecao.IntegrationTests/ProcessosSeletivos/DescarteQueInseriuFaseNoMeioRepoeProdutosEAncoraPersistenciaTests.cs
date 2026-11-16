namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoDescarteAposDeslocamentoDeOrdem;

/// <remarks>Contra Postgres real: o estrago do descarte é do identity map do EF e das linhas de produtos da fase.</remarks>
public sealed class DescarteQueInseriuFaseNoMeioRepoeProdutosEAncoraPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public DescarteQueInseriuFaseNoMeioRepoeProdutosEAncoraPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Descartar a sessão que inseriu uma fase no meio repõe os produtos nas fases que os congelaram, sem duplicar identidade")]
    public async Task Descarte_AposInserirFaseNoMeio_ReporProdutosSemColidirNoIdentityMap()
    {
        CenarioDoDeslocamentoDeOrdem cenario = new(_fixture);
        await cenario.PublicarAsync("PS deslocamento");
        await cenario.InserirHomologacaoNoMeioAsync();
        await ConferirQueOsProdutosSobreviveramAoDeslocamentoAsync(cenario);

        Func<Task> descartar = cenario.DescartarAsync;

        await descartar.Should().NotThrowAsync(
            "deixar o Id congelado de um produto entrar numa fase que não é a que o congelou põe a MESMA " +
            "linha em duas fases dentro da mesma transação — a rastreada saindo de uma como órfã e a " +
            "congelada entrando na outra —, e o EF derruba o descarte inteiro, deixando o operador sem " +
            "como desfazer o deslocamento");
        ConferirCronogramaRepostoSemProdutoDuplicado(await cenario.CarregarEmContextoNovoAsync());
    }

    [Fact(DisplayName = "Descartar a sessão que inseriu uma fase no meio deixa a âncora do recurso resolvendo para o produto preliminar reposto")]
    public async Task Descarte_AposInserirFaseNoMeio_AncoraDoRecursoSegueOProdutoPreliminar()
    {
        CenarioDoDeslocamentoDeOrdem cenario = new(_fixture, comRecurso: true);
        await cenario.PublicarAsync("PS âncora deslocada");
        await cenario.InserirHomologacaoNoMeioAsync();
        ConferirQueAAncoraAcompanhaOProdutoRastreado(await cenario.CarregarEmContextoNovoAsync(), cenario);

        await cenario.DescartarAsync();

        ConferirQueAAncoraResolveParaOProdutoPreliminarDestaFase(await cenario.CarregarEmContextoNovoAsync());
    }

    private static async Task ConferirQueOsProdutosSobreviveramAoDeslocamentoAsync(CenarioDoDeslocamentoDeOrdem cenario)
    {
        ProcessoSeletivo deslocado = await cenario.CarregarEmContextoNovoAsync();
        deslocado.CronogramaFases.Should().HaveCount(4, "pré-condição: a sessão inseriu a fase no meio");
        ProdutoDe(deslocado, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar).Id.Should().Be(cenario.ProdutoPreliminarId,
            "pré-condição: renumerar a fase preserva o Id do produto — é o que faz o Id congelado colidir no descarte");
    }

    private static void ConferirCronogramaRepostoSemProdutoDuplicado(ProcessoSeletivo reposto)
    {
        reposto.CronogramaFases.Should().HaveCount(3, "o descarte repõe o cronograma congelado");
        reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR").Produtos.Should().ContainSingle()
            .Which.AtoCodigo.Should().Be("RESULTADO_CLASSIFICACAO");
        reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_FINAL").Produtos.Should().ContainSingle()
            .Which.AtoCodigo.Should().Be("RESULTADO_CLASSIFICACAO");
        reposto.CronogramaFases.SelectMany(f => f.Produtos).Select(p => p.Id)
            .Should().OnlyHaveUniqueItems("nenhum produto pode existir em duas fases ao mesmo tempo");
        reposto.CronogramaFases.Should().OnlyContain(
            fase => fase.Produtos.All(p => p.FaseCronogramaId == fase.Id), "o produto pertence à fase que o declara");
    }

    private static void ConferirQueAAncoraAcompanhaOProdutoRastreado(ProcessoSeletivo deslocado, CenarioDoDeslocamentoDeOrdem cenario) =>
        AncoraDaFase(deslocado, "RESULTADO_PRELIMINAR").Should().Be(cenario.ProdutoPreliminarId,
            "a redefinição do cronograma reusa o produto rastreado, e a âncora tem de apontar para ele — " +
            "não para a instância que a coleção nova trouxe e o SaveChanges descartou");

    private static void ConferirQueAAncoraResolveParaOProdutoPreliminarDestaFase(ProcessoSeletivo reposto)
    {
        FaseCronograma preliminar = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR");
        preliminar.RegraRecurso.Should().NotBeNull("o descarte repõe a regra de recurso congelada");
        preliminar.RegraRecurso!.ProdutoAncoraId.Should().Be(
            preliminar.Produtos.Single(p => p.Papel == PapelProdutoFase.Preliminar).Id,
            "a âncora resolve para o produto preliminar DESTA fase depois da restauração — a identidade " +
            "congelada foi descartada junto com o produto que a carregava, e uma âncora deixada para trás " +
            "só apareceria quando um ato publicado fosse procurar o prazo que lhe corresponde");
        reposto.CronogramaFases.Where(f => f.RegraRecurso is not null).Should().OnlyContain(
            f => f.Produtos.Any(p => p.Id == f.RegraRecurso!.ProdutoAncoraId),
            "nenhuma regra de recurso pode ancorar fora dos produtos da própria fase");
    }

    private static Guid AncoraDaFase(ProcessoSeletivo processo, string codigo) =>
        processo.CronogramaFases.Single(f => f.Codigo == codigo).RegraRecurso!.ProdutoAncoraId;
}
