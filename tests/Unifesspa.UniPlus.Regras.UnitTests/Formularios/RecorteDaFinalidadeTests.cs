namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O formulário de uma finalidade sai com as regras dele e só com elas: as derivações que ele cita,
/// com o que elas citam, e os fatos de outra finalidade como pressupostos — e avalia como a definição
/// do processo inteiro, quando os pressupostos chegam como fatos conhecidos (ADR-0139).
/// </summary>
public sealed class RecorteDaFinalidadeTests
{
    private const string Habilitacao = "HABILITACAO";

    [Fact(DisplayName = "O recorte leva só as seções, os termos e as derivações que a finalidade cita")]
    public void Recorte_LevaSoOQueAFinalidadeCita()
    {
        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(Processo(), Habilitacao);

        recorte.Regras.Etapas.Select(static e => e.Codigo).Should().Equal("HABILITACAO:DOCUMENTOS");
        recorte.Regras.Termos.Select(static t => t.Codigo).Should().Equal("HABILITACAO:VERACIDADE");
        recorte.Regras.Derivacoes.Select(static d => d.FatoCodigo).Should().BeEquivalentTo(
            ["ESCOLA_PUBLICA", "EGRESSO_REDE_PUBLICA"], "a derivação citada leva junto a derivação de que ela depende");
        recorte.Pressupostos.Should().Equal(
            ["MODALIDADE_CONVOCACAO", "TIPO_ESCOLA"], "a resposta da inscrição e o fato do sistema chegam como já conhecidos");
    }

    [Theory(DisplayName = "O recorte avalia como o processo inteiro com os pressupostos conhecidos")]
    [InlineData("PUBLICA", "AC")]
    [InlineData("PRIVADA", "AC")]
    [InlineData("PUBLICA", "LB_EP")]
    public void Recorte_AvaliaComoOProcessoInteiro(string tipoEscola, string modalidade)
    {
        DefinicaoFormulario processo = Processo();
        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(processo, Habilitacao);
        Dictionary<string, FatoResolvido> conhecidos = new(StringComparer.Ordinal)
        {
            ["TIPO_ESCOLA"] = FatoResolvido.Resolvido(Json(tipoEscola)),
            ["MODALIDADE_CONVOCACAO"] = FatoResolvido.Resolvido(Json(modalidade)),
        };

        Result<AvaliacaoFormulario> doRecorte = recorte.Regras.Avaliar(Entrada(new(StringComparer.Ordinal), conhecidos));
        AvaliacaoFormulario doProcesso = AvaliadorFormulario.Avaliar(processo, Entrada(
            new(StringComparer.Ordinal) { ["TIPO_ESCOLA"] = Json(tipoEscola) },
            new(StringComparer.Ordinal) { ["MODALIDADE_CONVOCACAO"] = conhecidos["MODALIDADE_CONVOCACAO"] }));

        doRecorte.IsSuccess.Should().BeTrue(doRecorte.Error?.Message);
        Serializar(doRecorte.Value!.Itens).Should().Be(Serializar(doProcesso.Itens.Where(static i => i.EtapaCodigo.StartsWith(Habilitacao, StringComparison.Ordinal))));
        Serializar(doRecorte.Value!.Termos).Should().Be(Serializar(doProcesso.Termos.Where(static t => t.Codigo.StartsWith(Habilitacao, StringComparison.Ordinal))));
    }

    [Fact(DisplayName = "A oferta de valores sai só para os campos da finalidade")]
    public void Recorte_LevaSoAOfertaDosCamposDaFinalidade()
    {
        Dictionary<string, IReadOnlySet<string>> ofertas = new(StringComparer.Ordinal)
        {
            ["TIPO_ESCOLA"] = new HashSet<string>(["PUBLICA", "PRIVADA"], StringComparer.Ordinal),
            ["CERTIFICADO"] = new HashSet<string>(["EMITIDO", "PENDENTE"], StringComparer.Ordinal),
        };

        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(Processo(), Habilitacao, ofertas);

        recorte.Regras.Ofertas().Keys.Should().Equal("CERTIFICADO");
    }

    /// <summary>
    /// A inscrição pergunta o tipo de escola; a habilitação pede o certificado de quem estudou em escola
    /// pública e a declaração de quem foi convocado em vaga de escola pública. A derivação que nenhum
    /// formulário da habilitação cita fica de fora do recorte.
    /// </summary>
    private static DefinicaoFormulario Processo() => new(
        [
            new DefinicaoEtapa("INSCRICAO:ESCOLA", null, [new DefinicaoItem("TIPO_ESCOLA", null, Obrigatoriedade.Sempre, [])]),
            new DefinicaoEtapa("HABILITACAO:DOCUMENTOS", null,
            [
                new DefinicaoItem("CERTIFICADO", Quando("EGRESSO_REDE_PUBLICA", true), Obrigatoriedade.Sempre, []),
                new DefinicaoItem("DECLARACAO_ESCOLA", Quando("MODALIDADE_CONVOCACAO", "LB_EP"), Obrigatoriedade.Sempre, []),
            ]),
        ],
        [
            new DefinicaoTermo("INSCRICAO:EDITAL", null, Obrigatoriedade.Sempre),
            new DefinicaoTermo("HABILITACAO:VERACIDADE", null, Obrigatoriedade.Quando(Quando("ESCOLA_PUBLICA", true))),
        ],
        [
            Booleana("ESCOLA_PUBLICA", Quando("TIPO_ESCOLA", "PUBLICA")),
            Booleana("EGRESSO_REDE_PUBLICA", Quando("ESCOLA_PUBLICA", true)),
            Booleana("NAO_CITADO", Quando("TIPO_ESCOLA", "PRIVADA")),
        ]);

    private static RegrasDerivacaoFato Booleana(string fato, PredicadoDnf quando)
    {
        RegraDerivacao regra = RegraDerivacao.CriarBooleana(quando);
        return RegrasDerivacaoFato.CriarBooleana(fato, [regra], regra.FatosCitados).Value!;
    }

    private static PredicadoDnf Quando(string fato, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, Json(valor)).Value!)]).Value!;

    private static EntradaAvaliacaoFormulario Entrada(
        Dictionary<string, JsonElement> respostas, Dictionary<string, FatoResolvido> conhecidos) =>
        new(respostas, new HashSet<string>(StringComparer.Ordinal), conhecidos);

    private static JsonElement Json(object valor) => JsonSerializer.SerializeToElement(valor);

    private static string Serializar<T>(IEnumerable<T> valor) => JsonSerializer.Serialize(valor.ToList(), JsonSerializerOptions.Web);
}
