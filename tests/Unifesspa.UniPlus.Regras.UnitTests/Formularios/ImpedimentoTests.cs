namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A resposta do campo que impede a inscrição (UNI-REQ-0145): a avaliação diz se a resposta, com as
/// anteriores, ativa o impedimento; a forma recusa o impedimento que não cabe no campo, que não cita a
/// resposta do próprio campo em cada cláusula ou que não tem mensagem.
/// </summary>
public class ImpedimentoTests : TestesDeAvaliacao
{
    private const string Etapa = "DADOS";
    private const string Mensagem = "Quem tem vínculo com o PARFOR não pode se inscrever neste processo.";

    [Theory(DisplayName = "A resposta que cumpre a condição impede; a que não cumpre, o campo oculto e a resposta anterior pendente, não ou ainda não")]
    [InlineData(true, true, true, Ternario.Verdadeiro)]
    [InlineData(false, true, true, Ternario.Falso)]
    [InlineData(true, false, true, Ternario.Falso)]
    [InlineData(true, true, null, Ternario.Indeterminado)]
    public void Avaliar_Impedimento(bool parfor, bool exibido, bool? licenciatura, Ternario impedido)
    {
        PredicadoDnf exibicao = Predicado(("EXIBE_PARFOR", true));
        DefinicaoFormulario formulario = new(
            [
                new DefinicaoEtapa(Etapa, exibicao: null,
                [
                    Item("EXIBE_PARFOR"),
                    Item("LICENCIATURA"),
                    new DefinicaoItem("VINCULO_PARFOR", exibicao, Obrigatoriedade.Sempre, [],
                        new Impedimento(Predicado(("VINCULO_PARFOR", true), ("LICENCIATURA", true)), Mensagem)),
                ]),
            ],
            termos: [],
            derivacoes: []);
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["EXIBE_PARFOR"] = JsonSerializer.SerializeToElement(exibido),
            ["VINCULO_PARFOR"] = JsonSerializer.SerializeToElement(parfor),
        };
        if (licenciatura is { } valor)
        {
            respostas["LICENCIATURA"] = JsonSerializer.SerializeToElement(valor);
        }

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, new EntradaAvaliacaoFormulario(
            respostas, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)));

        avaliacao.Itens.Single(static i => i.FatoCodigo == "VINCULO_PARFOR").Impedido.Should().Be(impedido);
    }

    [Fact(DisplayName = "A citação da resposta do próprio campo não é dependência do item")]
    public void FatosCitados_ImpedimentoNaoDependeDoProprioCampo()
    {
        DefinicaoItem item = new("VINCULO_PARFOR", null, Obrigatoriedade.Sempre, [],
            new Impedimento(Predicado(("VINCULO_PARFOR", true), ("LICENCIATURA", true)), Mensagem));

        item.FatosCitados.Should().Equal("LICENCIATURA");
    }

    [Fact(DisplayName = "Impedimento em campo de texto, com cláusula sem o próprio campo e sem mensagem é recusado pelos três motivos")]
    public void Conferir_ImpedimentoMalFormado_RecusaOsTres()
    {
        PredicadoDnf semOProprioCampo = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (0, Condicao("NOME", "X")),
            (1, Condicao("LICENCIATURA", true)),
        ]).Value!;

        List<FieldError> erros = FormaDoItem.Conferir(
            "NOME", 0, "Nome", TipoRenderizacao.Texto, "NOME_PESSOA", null, [], Obrigatoriedade.Sempre, [],
            new Impedimento(semOProprioCampo, "  "));

        erros.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("impedimento", ItemFormularioErrorCodes.ImpedimentoNaoCabeNoCampo),
            ("impedimento.quando", ItemFormularioErrorCodes.ImpedimentoSemOProprioCampo),
            ("impedimento.mensagem", ItemFormularioErrorCodes.ImpedimentoMensagemInvalida),
        ]);
    }

    [Fact(DisplayName = "Impedimento sem cláusula é recusado: a condição é obrigatória")]
    public void Conferir_ImpedimentoSemClausula_Recusa()
    {
        List<FieldError> erros = FormaDoItem.Conferir(
            "VINCULO_PARFOR", 0, "Vínculo com o PARFOR", TipoRenderizacao.Booleano, null, null, [], Obrigatoriedade.Sempre, [],
            new Impedimento(PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, Mensagem));

        erros.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "impedimento.quando",
            Error = new { Code = ItemFormularioErrorCodes.ImpedimentoSemCondicao },
        });
    }

    [Theory(DisplayName = "A mensagem do impedimento tem até 500 caracteres")]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Conferir_TamanhoDaMensagem(int tamanho, bool aceita)
    {
        List<FieldError> erros = FormaDoItem.Conferir(
            "VINCULO_PARFOR", 0, "Vínculo com o PARFOR", TipoRenderizacao.Booleano, null, null, [], Obrigatoriedade.Sempre, [],
            new Impedimento(Predicado(("VINCULO_PARFOR", true)), new string('a', tamanho)));

        erros.Should().HaveCount(aceita ? 0 : 1);
    }

    [Fact(DisplayName = "O campo de grupo repetível não tem impedimento")]
    public void ConferirGrupo_CampoComImpedimento_Recusa()
    {
        List<FieldError> erros = FormaDoGrupo.Conferir(
            "COMPOSICAO", 1, "Composição familiar", 0, null, [("PARENTESCO", null, true)], [], Obrigatoriedade.Sempre);

        erros.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "subitens[0].impedimento",
            Error = new { Code = GrupoFormularioErrorCodes.CampoComImpedimento },
        });
    }

    private static DefinicaoItem Item(string fato) => new(fato, null, Obrigatoriedade.Nunca, []);

    private static PredicadoDnf Predicado(params (string Fato, object Valor)[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, Condicao(c.Fato, c.Valor)))]).Value!;

    private static CondicaoDnf Condicao(string fato, object valor) =>
        CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;
}
