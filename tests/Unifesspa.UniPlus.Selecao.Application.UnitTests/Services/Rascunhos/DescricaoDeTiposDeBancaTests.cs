namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class DescricaoDeTiposDeBancaTests
{
    private const string DocumentoComBancas = """
        { "cronograma": { "fases": [ { "chave": "a", "bancasRequeridas": [
            { "chave": "b1", "tipoBancaCodigo": "BANCA_ANALISE_RECURSOS" },
            { "chave": "b2", "tipoBancaCodigo": "BANCA_REMOVIDA" } ] } ] },
          "etapas": [ { "chave": "e", "bancas": [ { "chave": "b3", "tipoBancaCodigo": "BANCA_ANALISE_RECURSOS" } ] } ] }
        """;

    private readonly ITipoBancaReader _tipos = Substitute.For<ITipoBancaReader>();

    public DescricaoDeTiposDeBancaTests() =>
        _tipos.ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new TipoBancaView(
                Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursos, "Banca de análise de recursos", null, null,
                PapelDaBanca.JulgaAPrimeiraInstancia, false, false, [])]);

    [Fact(DisplayName = "O nome do tipo de banca entra na banca da fase e na da etapa, com uma leitura pelos códigos distintos")]
    public async Task Descrever_BancasDaFaseEDaEtapa_TrazONomeAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBancas, new DescricaoDeTiposDeBanca(_tipos));

        JsonNode? daFase = documento["cronograma"]!["fases"]![0]!["bancasRequeridas"]![0];
        JsonNode? daEtapa = documento["etapas"]![0]!["bancas"]![0];
        DocumentoDescritoDeTeste.Chaves(daFase).Should().Equal("chave", "tipoBancaCodigo", "tipoBancaNome");
        daFase!["tipoBancaNome"]!.GetValue<string>().Should().Be("Banca de análise de recursos");
        daEtapa!["tipoBancaNome"]!.GetValue<string>().Should().Be("Banca de análise de recursos");
        await _tipos.Received(1).ListarVivosPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo de banca que o cadastro não devolve vivo fica sem nome")]
    public async Task Descrever_TipoDeBancaSemCadastroVivo_OmiteONome()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBancas, new DescricaoDeTiposDeBanca(_tipos));

        documento["cronograma"]!["fases"]![0]!["bancasRequeridas"]![1]!["tipoBancaNome"].Should().BeNull();
    }
}
