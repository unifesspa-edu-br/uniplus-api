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
    private const string Pagina = "DADOS";
    private const string Mensagem = "Quem tem vínculo com o PARFOR não pode se inscrever neste processo.";

    [Theory(DisplayName = "A resposta que cumpre a condição impede; a que não cumpre, o campo oculto e a resposta anterior pendente, não ou ainda não")]
    [InlineData(true, true, true, Ternario.Verdadeiro)]
    [InlineData(false, true, true, Ternario.Falso)]
    [InlineData(true, false, true, Ternario.Falso)]
    [InlineData(true, true, null, Ternario.Indeterminado)]
    public void Avaliar_Impedimento(bool parfor, bool exibido, bool? licenciatura, Ternario impedido)
    {
        AvaliacaoFormulario avaliacao = AvaliarDefinicao(FormularioComVinculoImpeditivo(), new EntradaAvaliacaoFormulario(
            RespostasDoVinculo(parfor, exibido, licenciatura),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            Envio: null));

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

        List<FieldError> erros = ConferirCampo("NOME", TipoDeCampo.Texto, "NOME_PESSOA", new Impedimento(semOProprioCampo, "  "));

        erros.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regras.impedimento", ItemFormularioErrorCodes.ImpedimentoNaoCabeNoCampo),
            ("regras.impedimento.quando", ItemFormularioErrorCodes.ImpedimentoSemOProprioCampo),
            ("regras.impedimento.mensagem", ItemFormularioErrorCodes.ImpedimentoMensagemInvalida),
        ]);
    }

    [Fact(DisplayName = "Impedimento sem cláusula é recusado: a condição é obrigatória")]
    public void Conferir_ImpedimentoSemClausula_Recusa()
    {
        List<FieldError> erros = ConferirCampo(
            "VINCULO_PARFOR", TipoDeCampo.SimOuNao, formato: null, new Impedimento(PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, Mensagem));

        erros.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "regras.impedimento.quando",
            Error = new { Code = ItemFormularioErrorCodes.ImpedimentoSemCondicao },
        });
    }

    [Theory(DisplayName = "A mensagem do impedimento tem até 500 caracteres")]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Conferir_TamanhoDaMensagem(int tamanho, bool aceita)
    {
        List<FieldError> erros = ConferirCampo(
            "VINCULO_PARFOR", TipoDeCampo.SimOuNao, formato: null, new Impedimento(Predicado(("VINCULO_PARFOR", true)), new string('a', tamanho)));

        erros.Should().HaveCount(aceita ? 0 : 1);
    }

    [Fact(DisplayName = "O campo de grupo repetível não tem impedimento")]
    public void ConferirGrupo_CampoComImpedimento_Recusa()
    {
        List<FieldError> erros = FormaDoGrupo.Conferir(
            "COMPOSICAO", 1, "Composição familiar", new OcorrenciasDoGrupo(0, null, IncluiCandidato: false), [("PARENTESCO", null, true)], new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre));

        erros.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "subitens[0].regras.impedimento",
            Error = new { Code = GrupoFormularioErrorCodes.CampoComImpedimento },
        });
    }

    [Theory(DisplayName = "Só o formulário que serve à fase de inscrição admite impedimento, seja ele obrigatório ou não")]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void ConferirDeclaracao_ServeAInscricao_NaoRecusa(bool serveAFaseDeInscricao, bool obrigatorio) =>
        Impedimento.ConferirDeclaracao(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio), [ImpedimentoDoVinculo()])
            .Should().BeEmpty();

    [Theory(DisplayName = "O formulário que não serve à fase de inscrição recusa o impedimento na posição do item, e só dele")]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ConferirDeclaracao_NaoServeAInscricao_RecusaOItemComImpedimento(bool serveAFaseDeInscricao, bool obrigatorio)
    {
        List<FieldError> erros = Impedimento.ConferirDeclaracao(
            new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio), [null, ImpedimentoDoVinculo(), null]);

        erros.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[1].regras.impedimento",
            Error = new { Code = ItemFormularioErrorCodes.ImpedimentoForaDaInscricao },
        });
    }

    [Fact(DisplayName = "Sem declaração do formulário, o impedimento é recusado")]
    public void ConferirDeclaracao_SemDeclaracao_RecusaOImpedimento() =>
        Impedimento.ConferirDeclaracao(declaracao: null, [ImpedimentoDoVinculo()])
            .Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ImpedimentoForaDaInscricao);

    [Fact(DisplayName = "Item sem impedimento não é recusado em formulário que não serve à fase de inscrição")]
    public void ConferirDeclaracao_ItemSemImpedimento_NaoRecusa() =>
        Impedimento.ConferirDeclaracao(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: false, Obrigatorio: true), [null, null])
            .Should().BeEmpty();

    private static Impedimento ImpedimentoDoVinculo() => new(Predicado(("VINCULO_PARFOR", true)), Mensagem);

    private static DefinicaoFormulario FormularioComVinculoImpeditivo() =>
        new(
            [
                new DefinicaoPagina(Pagina, exibicao: null,
                [
                    Item("EXIBE_PARFOR"),
                    Item("LICENCIATURA"),
                    new DefinicaoItem("VINCULO_PARFOR", Predicado(("EXIBE_PARFOR", true)), Obrigatoriedade.Sempre, [],
                        new Impedimento(Predicado(("VINCULO_PARFOR", true), ("LICENCIATURA", true)), Mensagem)),
                ]),
            ],
            termos: [],
            derivacoes: []);

    private static Dictionary<string, JsonElement> RespostasDoVinculo(bool parfor, bool exibido, bool? licenciatura)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["EXIBE_PARFOR"] = JsonSerializer.SerializeToElement(exibido),
            ["VINCULO_PARFOR"] = JsonSerializer.SerializeToElement(parfor),
        };
        if (licenciatura is { } valor)
        {
            respostas["LICENCIATURA"] = JsonSerializer.SerializeToElement(valor);
        }

        return respostas;
    }

    private static List<FieldError> ConferirCampo(string fato, TipoDeCampo tipo, string? formato, Impedimento impedimento) =>
        FormaDoItem.Conferir(
            fato,
            0,
            ApresentacaoLidaDoCampo.De(new ApresentacaoDoCampo("Rótulo do campo", tipo)),
            formato,
            RegrasLidasDoCampo.De(new RegrasDoCampo(exibicao: null, Obrigatoriedade.Sempre, [], impedimento)));

    private static DefinicaoItem Item(string fato) => new(fato, null, Obrigatoriedade.Nunca, []);

    private static PredicadoDnf Predicado(params (string Fato, object Valor)[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, Condicao(c.Fato, c.Valor)))]).Value!;

    private static CondicaoDnf Condicao(string fato, object valor) =>
        CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;
}
