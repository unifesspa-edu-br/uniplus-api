namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DescricaoDeModalidadesTests
{
    private const string DocumentoComModalidades = """
        { "distribuicaoVagas": [ { "ofertaCursoCodigo": "MEDICINA_MARABA", "modalidades": [
            { "codigo": "AMPLA_CONCORRENCIA", "quantidadeDeclarada": 10 },
            { "codigo": "SEM_DESCRICAO" },
            { "codigo": "MODALIDADE_REMOVIDA" } ] } ] }
        """;

    private readonly Dictionary<string, ModalidadeNoCadastro> _modalidades = new(StringComparer.Ordinal)
    {
        ["AMPLA_CONCORRENCIA"] = Modalidade("AMPLA_CONCORRENCIA", "Vagas sem reserva"),
        ["SEM_DESCRICAO"] = Modalidade("SEM_DESCRICAO", null),
    };

    private static ModalidadeNoCadastro Modalidade(string codigo, string? descricao) =>
        ModalidadesNoCadastroDeTeste.Da(codigo, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { Descricao = descricao };

    [Fact(DisplayName = "A descrição da modalidade entra logo depois do código, sem alterar a quantidade declarada")]
    public async Task Descrever_ModalidadeComDescricao_TrazADescricaoAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComModalidades, new DescricaoDeModalidades(_modalidades));

        JsonNode? modalidade = documento["distribuicaoVagas"]![0]!["modalidades"]![0];
        DocumentoDescritoDeTeste.Chaves(modalidade).Should().Equal("codigo", "descricao", "quantidadeDeclarada");
        modalidade!["descricao"]!.GetValue<string>().Should().Be("Vagas sem reserva");
        modalidade["quantidadeDeclarada"]!.GetValue<int>().Should().Be(10);
    }

    [Fact(DisplayName = "A modalidade sem descrição no cadastro, ou que ele não devolve viva, fica sem o campo")]
    public async Task Descrever_ModalidadeSemDescricaoOuSemCadastroVivo_OmiteOCampo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComModalidades, new DescricaoDeModalidades(_modalidades));

        JsonArray modalidades = documento["distribuicaoVagas"]![0]!["modalidades"]!.AsArray();
        DocumentoDescritoDeTeste.Chaves(modalidades[1]).Should().Equal("codigo");
        DocumentoDescritoDeTeste.Chaves(modalidades[2]).Should().Equal("codigo");
    }
}
