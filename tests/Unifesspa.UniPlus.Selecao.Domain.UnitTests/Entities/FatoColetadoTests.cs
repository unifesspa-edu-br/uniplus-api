namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Story #559 — a apresentação do campo no formulário de inscrição (Rotulo/TipoRenderizacao/
/// Obrigatorio) na factory de <see cref="FatoColetado"/>. A coerência entre TipoRenderizacao e o
/// Dominio do fato no catálogo é validada na Application (cross-módulo) — coberta em
/// <c>DefinirFatosColetadosCommandHandlerTests</c>, não aqui.
/// </summary>
public sealed class FatoColetadoTests
{
    private static readonly CopiaDoItemNoCatalogo CopiaPadrao = ProtecaoDoFatoDeTeste.Copia();

    private static ApresentacaoDoCampo Campo(string rotulo, TipoRenderizacao tipo, string? ajuda = null) => new(rotulo, tipo, ajuda);

    private static RegrasDoCampo Regras(Obrigatoriedade? obrigatoriedade = null, IReadOnlyList<RestricaoValor>? restricoes = null) =>
        new(null, obrigatoriedade ?? Obrigatoriedade.Nunca, restricoes ?? []);

    [Fact(DisplayName = "Rótulo vazio é recusado")]
    public void Criar_RotuloVazio_RetornaFalha()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, null, Campo("", TipoRenderizacao.SelecaoUnica), Regras(), CopiaPadrao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.RotuloObrigatorio);
    }

    [Theory(DisplayName = "Classificação de proteção de dados fora do vocabulário fechado é recusada")]
    [InlineData("")]
    [InlineData("SEGREDO")]
    [InlineData("pessoal")]
    public void Criar_ClassificacaoProtecaoForaDoVocabulario_RetornaFalha(string classificacao)
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, null, Campo("Cor ou raça", TipoRenderizacao.SelecaoUnica), Regras(), ProtecaoDoFatoDeTeste.Copia(classificacao));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.ClassificacaoProtecaoInvalida);
    }

    [Fact(DisplayName = "Rótulo só de espaço é recusado")]
    public void Criar_RotuloSoEspaco_RetornaFalha()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, null, Campo("   ", TipoRenderizacao.SelecaoUnica), Regras(), CopiaPadrao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.RotuloObrigatorio);
    }

    [Theory(DisplayName = "O formato existe se, e só se, o campo é de texto")]
    [InlineData(TipoRenderizacao.Texto, null, false)]
    [InlineData(TipoRenderizacao.Texto, "CPF", true)]
    [InlineData(TipoRenderizacao.Booleano, "CPF", false)]
    [InlineData(TipoRenderizacao.Booleano, null, true)]
    public void Criar_FormatoSoEmCampoDeTexto(TipoRenderizacao tipo, string? formato, bool aceito)
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "NOME_SOCIAL", 0, null, Campo("Nome social", tipo), Regras(), ProtecaoDoFatoDeTeste.Copia(formato: formato));

        if (aceito)
        {
            resultado.Value!.Formato.Should().Be(formato);
        }
        else
        {
            resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.FormatoIncoerente);
        }
    }

    [Fact(DisplayName = "Obrigatoriedade que cita o próprio fato é recusada")]
    public void Criar_ObrigatoriedadeAutorreferente_Recusa()
    {
        Obrigatoriedade quando = Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("PCD", Operador.Igual, System.Text.Json.JsonSerializer.SerializeToElement(true)).Value!)]).Value!);

        FatoColetado.Criar("PCD", 0, null, Campo("Pessoa com deficiência", TipoRenderizacao.Booleano), Regras(quando), CopiaPadrao)
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.RegraAutorreferente);
    }

    [Theory(DisplayName = "O componente de exibição copiado do catálogo é do vocabulário fechado; sem declaração, é nulo")]
    [InlineData("MOEDA", true)]
    [InlineData("SITUACAO", true)]
    [InlineData(null, true)]
    [InlineData("REAIS", false)]
    public void Criar_ComponenteDeExibicao_SoDoVocabularioFechado(string? componente, bool aceito)
    {
        CopiaDoItemNoCatalogo copia = ProtecaoDoFatoDeTeste.Copia(componenteExibicao: componente);
        Result<FatoColetado> resultado = FatoColetado.Criar("RENDA_BRUTA_FAMILIAR", 0, null, Campo("Renda bruta familiar", TipoRenderizacao.Numero), Regras(), copia);

        resultado.IsSuccess.Should().Be(aceito);
        if (aceito)
        {
            (resultado.Value!.Descricao, resultado.Value.ComponenteExibicao).Should().Be((ProtecaoDoFatoDeTeste.Descricao, componente));
        }
        else
        {
            resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.ComponenteExibicaoInvalido);
        }
    }

    [Fact(DisplayName = "Ajuda acima do limite é recusada no campo")]
    public void Criar_AjudaLonga_Recusa() =>
        FatoColetado.Criar("PCD", 0, null, Campo("PCD", TipoRenderizacao.Booleano, new string('a', FormaDoItem.AjudaMaxLength + 1)), Regras(), CopiaPadrao)
            .Errors.Should().ContainSingle().Which.Field.Should().Be("ajuda");

    public static TheoryData<TipoRenderizacao, RestricaoValor, bool> RestricoesPorTipoDeCampo => new()
    {
        { TipoRenderizacao.Numero, new FaixaNumerica(0, 10), true },
        { TipoRenderizacao.Texto, new FaixaNumerica(0, 10), false },
        { TipoRenderizacao.Texto, new TamanhoTexto(1, 10), true },
        { TipoRenderizacao.Numero, new TamanhoTexto(1, 10), false },
        { TipoRenderizacao.SelecaoMultipla, new OpcoesDasRespostas(["OPCAO_CURSO_1"]), true },
        { TipoRenderizacao.Booleano, new OpcoesPermitidas([new OpcoesCondicionadas(null, ["SIM"])]), false },
    };

    [Theory(DisplayName = "A restrição cabe no tipo do campo: faixa no numérico, tamanho no de texto, opções no de seleção")]
    [MemberData(nameof(RestricoesPorTipoDeCampo))]
    public void Criar_RestricaoPorTipoDeCampo(TipoRenderizacao tipo, RestricaoValor restricao, bool aceita)
    {
        string? formato = tipo == TipoRenderizacao.Texto ? "LIVRE" : null;
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "CAMPO", 0, null, Campo("Campo", tipo), Regras(restricoes: [restricao]), ProtecaoDoFatoDeTeste.Copia(formato: formato));

        if (aceita)
        {
            resultado.Value!.Restricoes.Should().Equal(restricao);
        }
        else
        {
            resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
                new { Field = "restricoes[0]", Error = new { Code = ItemFormularioErrorCodes.RestricaoIncoerente } });
        }
    }

    [Fact(DisplayName = "Duas restrições do mesmo tipo no item são recusadas")]
    public void Criar_RestricaoRepetida_Recusa() =>
        FatoColetado.Criar(
                "IDADE", 0, null, Campo("Idade", TipoRenderizacao.Numero), Regras(restricoes: [new FaixaNumerica(0, null), new FaixaNumerica(null, 100)]), CopiaPadrao)
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(RestricaoValorErrorCodes.TipoRepetido);

    [Theory(DisplayName = "Limite da faixa com mais casas decimais do que o edital congela é recusado")]
    [InlineData("0.0001", true)]
    [InlineData("0.00001", false)]
    public void Criar_LimiteDaFaixaAlemDasCasasDecimais(string minimo, bool aceita) =>
        FatoColetado.Criar(
                "NOTA",
                0,
                null,
                Campo("Nota", TipoRenderizacao.Numero),
                Regras(restricoes: [new FaixaNumerica(decimal.Parse(minimo, System.Globalization.CultureInfo.InvariantCulture), null)]),
                CopiaPadrao)
            .IsSuccess.Should().Be(aceita);

    [Fact(DisplayName = "As opções permitidas entram nas condições do item como pertinência do próprio fato, para os vínculos de valor")]
    public void Condicoes_OpcoesPermitidas_CitamOsValoresDoProprioFato()
    {
        FatoColetado fato = FatoColetado.Criar(
            "COR_RACA",
            1,
            null,
            Campo("Cor ou raça", TipoRenderizacao.SelecaoUnica),
            Regras(restricoes: [new OpcoesPermitidas([new OpcoesCondicionadas(null, ["PRETA", "PARDA"])])]),
            CopiaPadrao).Value!;

        CondicaoDnf pertinencia = fato.Condicoes.Should().ContainSingle().Which;
        pertinencia.Fato.Should().Be("COR_RACA");
        pertinencia.Valor.EnumerateArray().Select(static v => v.GetString()).Should().Equal("PARDA", "PRETA");
    }

    [Fact(DisplayName = "Rótulo com espaços nas bordas é aparado")]
    public void Criar_RotuloComEspacos_EAparado()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, null, Campo("  Cor ou raça  ", TipoRenderizacao.SelecaoUnica), Regras(), CopiaPadrao);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Rotulo.Should().Be("Cor ou raça");
    }

    [Fact(DisplayName = "Fato válido com apresentação completa é aceito")]
    public void Criar_ApresentacaoCompleta_Aceita()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "BAIXA_RENDA", 0, null, Campo("Baixa renda", TipoRenderizacao.Booleano), Regras(Obrigatoriedade.Sempre), CopiaPadrao);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Rotulo.Should().Be("Baixa renda");
        resultado.Value!.TipoRenderizacao.Should().Be(TipoRenderizacao.Booleano);
        resultado.Value!.Obrigatoriedade.Should().Be(Obrigatoriedade.Sempre);
    }

    [Fact(DisplayName = "Código do fato acima do limite é recusado")]
    public void Criar_FatoCodigoMuitoLongo_Recusa()
    {
        string codigoLongo = new('A', FormaDoItem.FatoCodigoMaxLength + 1);

        Result<FatoColetado> resultado = FatoColetado.Criar(
            codigoLongo, 0, null, Campo("Rótulo", TipoRenderizacao.SelecaoUnica), Regras(), CopiaPadrao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.FatoCodigoTamanho);
    }

    [Fact(DisplayName = "Rótulo acima do limite é recusado")]
    public void Criar_RotuloMuitoLongo_Recusa()
    {
        string rotuloLongo = new('a', FormaDoItem.RotuloMaxLength + 1);

        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, null, Campo(rotuloLongo, TipoRenderizacao.SelecaoUnica), Regras(), CopiaPadrao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.RotuloTamanho);
    }

    [Fact(DisplayName = "ADR-0125: violações independentes acumulam num único lote")]
    public void Criar_OrdemNegativaERotuloVazio_AcumulaAsDuasViolacoes()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", -1, null, Campo("", TipoRenderizacao.SelecaoUnica), Regras(), CopiaPadrao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            ItemFormularioErrorCodes.OrdemInvalida,
            ItemFormularioErrorCodes.RotuloObrigatorio,
        ]);
    }
}
