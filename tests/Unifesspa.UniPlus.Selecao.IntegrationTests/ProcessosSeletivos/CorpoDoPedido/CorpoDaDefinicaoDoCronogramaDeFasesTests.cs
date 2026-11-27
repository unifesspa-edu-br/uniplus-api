namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpoDoPedido;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Xunit.Abstractions;

public sealed class CorpoDaDefinicaoDoCronogramaDeFasesTests(ITestOutputHelper saida)
{
    /// <summary>Os tipos de banca da semente do módulo Configuração, gravados por script SQL, sem lista em código.</summary>
    private const int TiposDeBancaSemeados = 10;

    [Fact]
    public void DefinicaoDoCronograma_ComTodasAsFasesBancasCategoriasEAtos_CabeNoLimiteDoCorpoComFolgaDeQuatroVezes()
    {
        DefinirCronogramaFasesCommand piorCaso = PiorCasoDoCronogramaDeFases.Definicao(
            FaseCanonicaSeed.Itens.Count, TiposDeBancaSemeados, CategoriaDocumentoSeed.Itens.Count, CodigosDosTiposDeAtoSemeados.Todos);

        int bytes = MedidorDoCorpoDoPedido.Bytes(piorCaso.Fases);
        saida.WriteLine($"Cronograma de fases, em bytes: pior caso válido {bytes}, com {piorCaso.Fases.Count} fases.");

        LimiteDoCorpoDoPedido.ComportaComFolga(bytes).Should().BeTrue();
    }

    [Fact]
    public void DefinicaoDoCronograma_NoPiorCaso_NaoRepeteFaseCanonicaNemOrdem()
    {
        DefinirCronogramaFasesCommand piorCaso = PiorCasoDoCronogramaDeFases.Definicao(
            FaseCanonicaSeed.Itens.Count, TiposDeBancaSemeados, CategoriaDocumentoSeed.Itens.Count, CodigosDosTiposDeAtoSemeados.Todos);

        piorCaso.Fases.Select(static f => f.FaseCanonicaId).Distinct().Should().HaveCount(piorCaso.Fases.Count);
        piorCaso.Fases.Select(static f => f.Ordem).Distinct().Should().HaveCount(piorCaso.Fases.Count);
    }
}
