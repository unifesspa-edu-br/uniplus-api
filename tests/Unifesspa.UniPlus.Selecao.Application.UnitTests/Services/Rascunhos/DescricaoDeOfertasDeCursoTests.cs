namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DescricaoDeOfertasDeCursoTests
{
    private const string DocumentoComDistribuicoes = """
        { "distribuicaoVagas": [
            { "ofertaCursoCodigo": "MEDICINA_MARABA" },
            { "ofertaCursoCodigo": "OFERTA_REMOVIDA" },
            { "ofertaCursoCodigo": "MEDICINA_MARABA" } ] }
        """;

    private readonly Dictionary<string, OfertaDeCursoCitada> _ofertas = new(StringComparer.Ordinal)
    {
        ["MEDICINA_MARABA"] = Oferta("MEDICINA_MARABA", "Medicina", "Bacharelado", "Campus Universitário de Marabá", "MATUTINO", "VESPERTINO"),
        ["PEDAGOGIA_MARABA"] = Oferta("PEDAGOGIA_MARABA", "Pedagogia", "Licenciatura", "Campus Universitário de Marabá", "NOTURNO"),
    };

    private static OfertaDeCursoCitada Oferta(string codigo, string curso, string grau, string unidade, params string[] turnos) =>
        new(Guid.CreateVersion7(), codigo, curso, grau, unidade, turnos, null, null, null);

    [Fact(DisplayName = "O curso, o grau, a unidade ofertante e os turnos entram depois do código da oferta, com os turnos na ordem do cadastro")]
    public async Task Descrever_OfertaViva_TrazOsDadosDaOfertaAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComDistribuicoes, new DescricaoDeOfertasDeCurso(_ofertas));

        JsonNode? distribuicao = documento["distribuicaoVagas"]![0];
        DocumentoDescritoDeTeste.Chaves(distribuicao).Should().Equal(
            "ofertaCursoCodigo", "cursoNome", "cursoGrau", "unidadeOfertanteNome", "turnos");
        distribuicao!["cursoNome"]!.GetValue<string>().Should().Be("Medicina");
        distribuicao["cursoGrau"]!.GetValue<string>().Should().Be("Bacharelado");
        distribuicao["unidadeOfertanteNome"]!.GetValue<string>().Should().Be("Campus Universitário de Marabá");
        DocumentoDescritoDeTeste.Textos(distribuicao["turnos"]).Should().Equal("MATUTINO", "VESPERTINO");
    }

    [Fact(DisplayName = "A oferta que o cadastro não devolve viva fica sem nenhum dos descritivos")]
    public async Task Descrever_OfertaSemCadastroVivo_OmiteOsDescritivos()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComDistribuicoes, new DescricaoDeOfertasDeCurso(_ofertas));

        DocumentoDescritoDeTeste.Chaves(documento["distribuicaoVagas"]![1]).Should().Equal("ofertaCursoCodigo");
    }

    [Fact(DisplayName = "A oferta repetida no documento é descrita nas duas distribuições")]
    public async Task Descrever_OfertaRepetida_DescreveAsDuas()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComDistribuicoes, new DescricaoDeOfertasDeCurso(_ofertas));

        documento["distribuicaoVagas"]![2]!["cursoNome"]!.GetValue<string>().Should().Be("Medicina");
    }
}
