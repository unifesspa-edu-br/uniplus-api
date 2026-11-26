namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Contrato do código, do rótulo e da descrição que cada tipo de instrumento normativo declara.</summary>
public sealed class TipoInstrumentoNormativoTests
{
    [Fact(DisplayName = "O catálogo declara código, rótulo e descrição de cada tipo, na ordem do enum")]
    public void Catalogo_DeclaraCodigoRotuloEDescricaoDeCadaTipo()
    {
        CatalogoDe<TipoInstrumentoNormativo>.Todos().Select(static d => (d.Codigo, d.Rotulo, d.Descricao)).Should().Equal(
            ("LEI", "Lei", "Ato normativo de maior hierarquia no ordenamento infraconstitucional, emanado do Poder Legislativo."),
            ("DECRETO", "Decreto", "Ato normativo do Poder Executivo que regulamenta leis ou dispõe sobre matéria de sua competência."),
            ("PORTARIA", "Portaria", "Ato administrativo de autoridade pública que disciplina procedimentos internos ou matéria de competência específica."),
            ("RESOLUCAO", "Resolução", "Ato normativo de órgão colegiado (conselho, câmara, comitê) com efeitos internos ou externos conforme a norma instituidora."),
            ("INSTRUCAO_NORMATIVA", "Instrução Normativa", "Ato administrativo de caráter técnico-operacional que detalha a execução de normas superiores."),
            ("PARECER", "Parecer", "Manifestação técnica ou jurídica de órgão competente que orienta a aplicação de normas — vinculante quando aprovado pela autoridade competente."),
            ("EDITAL", "Edital", "Ato de convocação do processo seletivo que fixa as regras do certame, inclusive o Fator de Atenuação das Desigualdades de Acesso (FADA) que ele institui."));
    }

    [Fact(DisplayName = "Os valores seguem a ordem declarada, da lei ao edital")]
    public void Valores_SeguemAOrdemDeclarada() =>
        CatalogoDe<TipoInstrumentoNormativo>.Valores().Should().Equal(
            TipoInstrumentoNormativo.Lei,
            TipoInstrumentoNormativo.Decreto,
            TipoInstrumentoNormativo.Portaria,
            TipoInstrumentoNormativo.Resolucao,
            TipoInstrumentoNormativo.InstrucaoNormativa,
            TipoInstrumentoNormativo.Parecer,
            TipoInstrumentoNormativo.Edital);

    [Theory(DisplayName = "O código declarado volta ao mesmo tipo")]
    [InlineData("LEI", TipoInstrumentoNormativo.Lei)]
    [InlineData("INSTRUCAO_NORMATIVA", TipoInstrumentoNormativo.InstrucaoNormativa)]
    [InlineData("EDITAL", TipoInstrumentoNormativo.Edital)]
    public void TentarObter_CodigoDeclarado_DevolveOTipo(string codigo, TipoInstrumentoNormativo esperado)
    {
        CatalogoDe<TipoInstrumentoNormativo>.TentarObter(codigo, out TipoInstrumentoNormativo tipo).Should().BeTrue();
        tipo.Should().Be(esperado);
    }

    [Theory(DisplayName = "Código desconhecido, em outra caixa, vazio ou nulo não é tipo")]
    [InlineData("MEDIDA_PROVISORIA")]
    [InlineData("lei")]
    [InlineData("")]
    [InlineData(null)]
    public void TentarObter_CodigoDesconhecido_NaoDevolveTipo(string? codigo) =>
        CatalogoDe<TipoInstrumentoNormativo>.TentarObter(codigo, out _).Should().BeFalse();

    [Fact(DisplayName = "Código desconhecido lido do banco falha em vez de virar tipo")]
    public void Obter_CodigoDesconhecido_Falha() =>
        FluentActions.Invoking(static () => CatalogoDe<TipoInstrumentoNormativo>.Obter("MEDIDA_PROVISORIA"))
            .Should().Throw<ArgumentException>();

    [Fact(DisplayName = "O enum passa na conferência dos códigos de contrato")]
    public void ConferirEnumsDe_DominioDaConfiguracao_Passa() =>
        FluentActions.Invoking(static () => EnumsDeContrato.ConferirEnumsDe([typeof(TipoInstrumentoNormativo).Assembly]))
            .Should().NotThrow();
}
