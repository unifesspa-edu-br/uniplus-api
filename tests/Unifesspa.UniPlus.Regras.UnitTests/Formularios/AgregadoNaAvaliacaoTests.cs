namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O agregado sobre um grupo repetível é um fato do formulário, resolvido em ordem como os derivados:
/// depois do grupo que ele agrega e antes do que o cita, mesmo quando o próprio grupo depende de outro
/// agregado (UNI-REQ-0146, ADR-0138).
/// </summary>
public class AgregadoNaAvaliacaoTests : TestesDeAvaliacao
{
    private const string Etapa = "HABILITACAO";

    [Theory(DisplayName = "O agregado de um grupo exibido por outro agregado é resolvido antes do item que o cita")]
    [InlineData(true, Ternario.Verdadeiro)]
    [InlineData(false, Ternario.Falso)]
    public void Avaliar_CadeiaDeAgregados_ResolveEmOrdem(bool trabalhaNoCampo, Ternario declaracaoVisivel)
    {
        // A declaração cita o agregado de B, B é exibido pelo agregado de A, e os itens vêm antes
        // dos grupos na etapa: só a ordem dos agregados no grafo resolve a cadeia numa passada.
        DefinicaoFormulario formulario = new(
            [
                new DefinicaoEtapa(Etapa, exibicao: null,
                    [Item("DECLARACAO_RURAL", Quando("PROPRIEDADE_NA_FAMILIA", true))],
                    [
                        Grupo("FAMILIA", exibicao: null, Item("TRABALHA_NO_CAMPO")),
                        Grupo("PROPRIEDADES", Quando("RURAL_NA_FAMILIA", true), Item("TEM_PROPRIEDADE")),
                    ]),
            ],
            termos: [],
            derivacoes: [],
            [
                new DefinicaoAgregado("RURAL_NA_FAMILIA", "FAMILIA", "TRABALHA_NO_CAMPO", OperacaoAgregado.Existe),
                new DefinicaoAgregado("PROPRIEDADE_NA_FAMILIA", "PROPRIEDADES", "TEM_PROPRIEDADE", OperacaoAgregado.Existe),
            ]);

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, new EntradaAvaliacaoFormulario(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new HashSet<string>([Etapa], StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal)
            {
                ["FAMILIA"] = [Ocorrencia("M1", "TRABALHA_NO_CAMPO", trabalhaNoCampo)],
                ["PROPRIEDADES"] = [Ocorrencia("P1", "TEM_PROPRIEDADE", true)],
            }));

        avaliacao.Itens.Single().Visivel.Should().Be(declaracaoVisivel);
        avaliacao.Fatos["PROPRIEDADE_NA_FAMILIA"].Valor!.Value.GetBoolean().Should().Be(trabalhaNoCampo);
    }

    [Fact(DisplayName = "O agregado sobre um grupo que o formulário não tem é recusado na definição")]
    public void Definicao_AgregadoSemGrupo_Recusa()
    {
        Action definir = () => _ = new DefinicaoFormulario(
            [new DefinicaoEtapa(Etapa, exibicao: null, [])],
            termos: [],
            derivacoes: [],
            [new DefinicaoAgregado("RURAL_NA_FAMILIA", "FAMILIA", "TRABALHA_NO_CAMPO", OperacaoAgregado.Existe)]);

        definir.Should().Throw<ArgumentException>().WithMessage("*FAMILIA*");
    }

    private static DefinicaoItem Item(string fato, PredicadoDnf? exibicao = null) => new(fato, exibicao, Obrigatoriedade.Sempre, []);

    private static DefinicaoGrupo Grupo(string codigo, PredicadoDnf? exibicao, DefinicaoItem subitem) =>
        new(codigo, exibicao, Obrigatoriedade.Nunca, 0, null, [subitem]);

    private static OcorrenciaRespondida Ocorrencia(string id, string fato, bool valor) =>
        new(id, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [fato] = JsonSerializer.SerializeToElement(valor) });

    private static PredicadoDnf Quando(string fato, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;
}
