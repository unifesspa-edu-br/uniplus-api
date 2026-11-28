namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.PesoAreaEnemDeTeste;

public sealed class PesoAreaEnemAreasTests
{
    [Theory(DisplayName = "Criar com grupo fora do domínio falha")]
    [InlineData("Engenharias")]
    [InlineData("Humanística III")]
    public void Criar_GrupoInvalido_Falha(string grupo)
    {
        Result<PesoAreaEnem> resultado = Criar(grupo: grupo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.GrupoCursoInvalido);
    }

    [Theory(DisplayName = "Área fora das cinco é recusada no campo do código, listando as aceitas")]
    [InlineData("FISICA")]
    [InlineData("redacao")]
    [InlineData("Redação")]
    [InlineData(null)]
    public void Criar_AreaForaDoDominio_Falha(string? codigo)
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(2, new(codigo, 1m, null)));

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio);
        erro.Field.Should().Be("areas[2].codigo");
        erro.Error.Message.Should().Contain("CIENCIAS_HUMANAS (Ciências Humanas e suas Tecnologias)");
        resultado.Errors.Should().Contain(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaFaltando);
    }

    [Fact]
    public void Criar_AreaDesconhecida_NaoRepeteCodigoEListaAreasAceitas()
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(2, new("AREA_DE_TESTE", 1m, null)));

        FieldError erro = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio);
        erro.Field.Should().Be("areas[2].codigo");
        erro.Error.Message.Should().NotContain("AREA_DE_TESTE");
        foreach (AreaEnemDescrita area in PesoAreaEnem.Areas)
        {
            erro.Error.Message.Should().Contain(area.Codigo).And.Contain(area.Rotulo);
        }
    }

    [Fact(DisplayName = "Área repetida é recusada no campo da repetição, e a área que faltou é nomeada")]
    public void Criar_AreaRepetida_Falha()
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(4, new(PesoAreaEnem.CodigoRedacao, 1m, null)));

        resultado.IsFailure.Should().BeTrue();
        FieldError repetida = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaRepetida);
        repetida.Field.Should().Be("areas[4].codigo");
        FieldError faltando = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaFaltando);
        faltando.Field.Should().Be("areas");
        faltando.Error.Message.Should().Contain(PesoAreaEnem.CodigoMatematica);
    }

    [Fact(DisplayName = "Área faltando é recusada no campo das áreas, nomeando só a que faltou")]
    public void Criar_AreaFaltando_Falha()
    {
        List<AreaInformada> areas = AreasValidas();
        areas.RemoveAt(3);

        Result<PesoAreaEnem> resultado = Criar(areas);

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Single();
        erro.Field.Should().Be("areas");
        erro.Error.Code.Should().Be(PesoAreaEnemErrorCodes.AreaFaltando);
        erro.Error.Message.Should().Contain(PesoAreaEnem.CodigoLinguagens);
        erro.Error.Message.Should().NotContain(PesoAreaEnem.CodigoMatematica);
    }

    [Fact(DisplayName = "Sem áreas, a recusa nomeia as cinco")]
    public void Criar_SemAreas_NomeiaAsCinco()
    {
        Result<PesoAreaEnem> resultado = PesoAreaEnem.Criar(Resolucao, Grupo, [], BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Single();
        erro.Error.Code.Should().Be(PesoAreaEnemErrorCodes.AreaFaltando);
        foreach (AreaEnemDescrita area in PesoAreaEnem.Areas)
        {
            erro.Error.Message.Should().Contain(area.Codigo);
        }
    }

    [Fact(DisplayName = "Áreas nulas são tratadas como nenhuma área informada")]
    public void Criar_AreasNulas_Falha()
    {
        Result<PesoAreaEnem> resultado = PesoAreaEnem.Criar(Resolucao, Grupo, null, BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaFaltando);
    }

    [Fact(DisplayName = "Área sem o peso é recusada no campo do peso da área, sem virar peso zero")]
    public void Criar_AreaSemPeso_Falha()
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(1, new AreaInformada(PesoAreaEnem.CodigoCienciasDaNatureza, null, null)));

        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "areas[1].peso" && e.Error.Code == PesoAreaEnemErrorCodes.PesoObrigatorio);
    }

    [Theory(DisplayName = "Peso fora da faixa é recusado no campo do peso da área")]
    [InlineData(-0.01, PesoAreaEnemErrorCodes.PesoNegativo)]
    [InlineData(100, PesoAreaEnemErrorCodes.PesoExcedeMaximo)]
    public void Criar_PesoForaDaFaixa_Falha(double peso, string codigoEsperado)
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(4, new(PesoAreaEnem.CodigoMatematica, (decimal)peso, null)));

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Single();
        erro.Field.Should().Be("areas[4].peso");
        erro.Error.Code.Should().Be(codigoEsperado);
        erro.Error.Message.Should().Contain("Matemática e suas Tecnologias");
    }

    [Theory(DisplayName = "Peso nos limites da faixa é aceito")]
    [InlineData(0)]
    [InlineData(99.99)]
    public void Criar_PesoNosLimites_Aceita(double peso)
    {
        Criar(ComArea(4, new(PesoAreaEnem.CodigoMatematica, (decimal)peso, null))).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Pesos das cinco áreas que somam zero, mesmo depois de arredondar, são recusados")]
    [InlineData(0)]
    [InlineData(0.004)]
    public void Criar_PesosSomamZero_Falha(double peso)
    {
        List<AreaInformada> areas =
        [
            .. PesoAreaEnem.Areas.Select(a => new AreaInformada(a.Codigo, (decimal)peso, null)),
        ];

        Result<PesoAreaEnem> resultado = Criar(areas);

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Single();
        erro.Field.Should().Be("areas");
        erro.Error.Code.Should().Be(PesoAreaEnemErrorCodes.PesosSomamZero);
    }

    [Theory(DisplayName = "Corte fora da faixa é recusado no campo do corte da área")]
    [InlineData(-1, PesoAreaEnemErrorCodes.CorteNegativo)]
    [InlineData(1000.001, PesoAreaEnemErrorCodes.CorteExcedeMaximo)]
    public void Criar_CorteForaDaFaixa_Falha(double corte, string codigoEsperado)
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(0, new(PesoAreaEnem.CodigoRedacao, 2m, (decimal)corte)));

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Single();
        erro.Field.Should().Be("areas[0].corte");
        erro.Error.Code.Should().Be(codigoEsperado);
    }

    [Theory(DisplayName = "Corte nos limites da faixa é aceito na Redação")]
    [InlineData(0)]
    [InlineData(1000)]
    public void Criar_CorteNosLimites_Aceita(double corte)
    {
        Criar(ComArea(0, new(PesoAreaEnem.CodigoRedacao, 2m, (decimal)corte))).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Corte em qualquer área do ENEM é aceito, porque a eliminação aplica corte por área")]
    [InlineData(1, PesoAreaEnem.CodigoCienciasDaNatureza)]
    [InlineData(4, PesoAreaEnem.CodigoMatematica)]
    public void Criar_CorteForaDaRedacao_Aceita(int indice, string codigo)
    {
        PesoAreaEnem peso = Criar(ComArea(indice, new(codigo, 1m, 450m))).Value!;

        peso.AreasDaLinha[indice].Corte.Should().Be(450m);
    }

    [Fact(DisplayName = "Base legal ausente é recusada")]
    public void Criar_SemBaseLegal_Falha()
    {
        Result<PesoAreaEnem> resultado = Criar(baseLegal: "  ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.BaseLegalObrigatoria);
    }

    [Fact(DisplayName = "Base legal acima de 500 caracteres é recusada")]
    public void Criar_BaseLegalLonga_Falha()
    {
        Result<PesoAreaEnem> resultado = Criar(baseLegal: new string('x', 501));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.BaseLegalTamanho);
    }

    [Fact(DisplayName = "Criar acumula as violações independentes de todos os campos")]
    public void Criar_AcumulaViolacoes()
    {
        List<AreaInformada> areas = ComArea(1, new(PesoAreaEnem.CodigoCienciasDaNatureza, -1m, null));
        areas[3] = new("FISICA", 1m, null);
        Result<PesoAreaEnem> resultado = Criar(areas, resolucao: "", grupo: "Engenharias", baseLegal: null);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("resolucao", PesoAreaEnemErrorCodes.ResolucaoObrigatoria),
            ("grupoCurso", PesoAreaEnemErrorCodes.GrupoCursoInvalido),
            ("areas[1].peso", PesoAreaEnemErrorCodes.PesoNegativo),
            ("areas[3].codigo", PesoAreaEnemErrorCodes.AreaForaDoDominio),
            ("areas", PesoAreaEnemErrorCodes.AreaFaltando),
            ("baseLegal", PesoAreaEnemErrorCodes.BaseLegalObrigatoria),
        ]);
    }

    [Theory(DisplayName = "Código da área com espaço em volta ou em outra caixa é recusado no campo do código")]
    [InlineData("  MATEMATICA ")]
    [InlineData("matematica")]
    public void Criar_CodigoForaDoFormato_Recusa(string codigo)
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(4, new(codigo, 1.50m, null)));

        FieldError erro = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio);
        erro.Field.Should().Be("areas[4].codigo");
        erro.Error.Message.Should().NotContain(codigo);
    }

    [Fact(DisplayName = "Corte fora da faixa numa área que não é a Redação é recusado pela faixa")]
    public void Criar_CorteNegativoForaDaRedacao_RecusaPelaFaixa()
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(3, new(PesoAreaEnem.CodigoLinguagens, 2.50m, -1m)));

        FieldError erro = resultado.Errors.Single();
        erro.Field.Should().Be("areas[3].corte");
        erro.Error.Code.Should().Be(PesoAreaEnemErrorCodes.CorteNegativo);
    }

    [Fact(DisplayName = "Código recusado longo volta na mensagem limitado, não inteiro")]
    public void Criar_CodigoLongo_MensagemLimitada()
    {
        string codigo = new('X', 5000);

        Result<PesoAreaEnem> resultado = Criar(ComArea(2, new(codigo, 1m, null)));

        string mensagem = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio).Error.Message;
        mensagem.Should().NotContain(codigo);
        mensagem.Should().NotContain(new string('X', 40));
    }

    [Fact(DisplayName = "Código só com espaços pede o código da área")]
    public void Criar_CodigoEmBranco_PedeOCodigo()
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(2, new("   ", 1m, null)));

        resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio)
            .Error.Message.Should().StartWith("Informe o código da área");
    }

    [Fact(DisplayName = "Lista de áreas com itens demais é recusada inteira, com um erro só no campo das áreas")]
    public void Criar_ItensDemais_UmaRecusaSo()
    {
        List<AreaInformada> areas = [.. Enumerable.Range(0, 5000).Select(static _ => new AreaInformada("X", 1m, null))];

        Result<PesoAreaEnem> resultado = Criar(areas);

        FieldError erro = resultado.Errors.Single();
        erro.Field.Should().Be("areas");
        erro.Error.Code.Should().Be(PesoAreaEnemErrorCodes.AreasEmExcesso);
        erro.Error.Message.Should().Contain("5000");
    }

    [Fact(DisplayName = "Itens demais não escondem a recusa da base legal")]
    public void Criar_ItensDemais_MantemRecusaDaBaseLegal()
    {
        List<AreaInformada> areas = [.. Enumerable.Range(0, 30).Select(static _ => new AreaInformada("X", 1m, null))];

        Result<PesoAreaEnem> resultado = Criar(areas, baseLegal: null);

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
            [PesoAreaEnemErrorCodes.AreasEmExcesso, PesoAreaEnemErrorCodes.BaseLegalObrigatoria]);
    }

    [Theory(DisplayName = "As mensagens de peso e de corte nomeiam a área em português correto, inclusive sem área reconhecida")]
    [InlineData("MATEMATICA", -1, "O peso informado para Matemática e suas Tecnologias não pode ser negativo.")]
    [InlineData("FISICA", -1, "O peso informado para esta área não pode ser negativo.")]
    public void Criar_MensagemDePeso_EmPortuguesCorreto(string codigo, double peso, string esperada)
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(4, new(codigo, (decimal)peso, null)));

        resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.PesoNegativo)
            .Error.Message.Should().Be(esperada);
    }

    [Theory(DisplayName = "As mensagens de corte nomeiam a área em português correto, inclusive sem área reconhecida")]
    [InlineData(0, "REDACAO", -1, PesoAreaEnemErrorCodes.CorteNegativo, "O corte informado para Redação não pode ser negativo.")]
    [InlineData(0, "REDACAO", 1000.001, PesoAreaEnemErrorCodes.CorteExcedeMaximo, "O corte informado para Redação não pode exceder 1000 (nota máxima de uma área do ENEM).")]
    [InlineData(4, "FISICA", -1, PesoAreaEnemErrorCodes.CorteNegativo, "O corte informado para esta área não pode ser negativo.")]
    public void Criar_MensagemDeCorte_EmPortuguesCorreto(int indice, string codigo, double corte, string erroEsperado, string esperada)
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(indice, new(codigo, 1m, (decimal)corte)));

        resultado.Errors.Single(e => e.Error.Code == erroEsperado).Error.Message.Should().Be(esperada);
    }

    [Fact(DisplayName = "Área repetida continua conferida pela regra da própria área")]
    public void Criar_AreaRepetida_ValidaPelaRegraDaArea()
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(4, new(PesoAreaEnem.CodigoLinguagens, 1m, 450m)));

        resultado.Errors.Should().Contain(e => e.Field == "areas[4].codigo" && e.Error.Code == PesoAreaEnemErrorCodes.AreaRepetida);
        resultado.Errors.Should().NotContain(e => e.Field == "areas[4].corte", "o corte dentro da faixa vale para qualquer área");
    }

    [Fact(DisplayName = "Código recusado não volta com caracteres de controle nem de formatação bidi")]
    public void Criar_CodigoComControle_NaoEcoaControle()
    {
        Result<PesoAreaEnem> resultado = Criar(ComArea(2, new("AB\nCD\u202EEF\u2028GH\u2029IJ", 1m, null)));

        string mensagem = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio).Error.Message;
        mensagem.Should().NotContain("AB?CD?EF?GH?IJ");
        mensagem.Should().NotContain("\n").And.NotContain("\u202E").And.NotContain("\u2028").And.NotContain("\u2029");
    }

    [Fact(DisplayName = "Código recusado não volta com caractere de formatação fora do plano básico")]
    public void Criar_CodigoComTagForaDoPlanoBasico_NaoEcoaATag()
    {
        // U+E0041 é um caractere de formatação (tag) representado por um par substituto: a
        // classificação por unidade UTF-16 veria só as duas metades e o deixaria passar.
        Result<PesoAreaEnem> resultado = Criar(ComArea(2, new("AB\U000E0041CD", 1m, null)));

        string mensagem = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio).Error.Message;
        mensagem.Should().NotContain("AB?CD");
        mensagem.Should().NotContain("\U000E0041");
    }

    [Fact(DisplayName = "Não-caractere na resolução ou na base legal é recusado como caractere inválido, sem exceção da normalização")]
    public void Criar_NaoCaractere_RecusaSemExcecao()
    {
        string comNaoCaractere = "Res. 805" + (char)0xFFFE;

        Criar(resolucao: comNaoCaractere).Errors.Should().ContainSingle(e =>
            e.Field == "resolucao" && e.Error.Code == PesoAreaEnemErrorCodes.ResolucaoCaractereInvalido);
        Criar(baseLegal: comNaoCaractere).Errors.Should().ContainSingle(e =>
            e.Field == "baseLegal" && e.Error.Code == PesoAreaEnemErrorCodes.BaseLegalCaractereInvalido);
        PesoAreaEnem.NormalizarResolucao(comNaoCaractere).Should().BeNull();
    }

    [Fact(DisplayName = "Base legal maior que o teto é recusada pelo tamanho, antes de ser varrida")]
    public void Criar_BaseLegalMuitoGrande_RecusaPeloTamanho() =>
        Criar(baseLegal: new string('B', 1_000_000) + "\n1").Errors.Should().ContainSingle(e =>
            e.Field == "baseLegal" && e.Error.Code == PesoAreaEnemErrorCodes.BaseLegalTamanho,
            "a quebra de linha no fim daria caractere inválido se o texto fosse varrido");

    [Fact(DisplayName = "Base legal aceita tabulação, quebra de linha e hífen condicional, comuns em texto jurídico colado")]
    public void Criar_BaseLegalComCaracteresDeTextoColado_Aceita()
    {
        string colada = "Resolução nº 805/2024,\tAnexo I\nart. 5º, inci\u00ADso II";

        PesoAreaEnem peso = Criar(baseLegal: colada).Value!;

        peso.BaseLegal.Should().Be(colada);
    }

    [Fact(DisplayName = "Base legal com o caractere nulo é recusada, embora aceite os demais invisíveis")]
    public void Criar_BaseLegalComCaractereNulo_Recusa() =>
        Criar(baseLegal: "Resolução nº 805\u0000/2024").Errors.Should().ContainSingle(e =>
            e.Field == "baseLegal" && e.Error.Code == PesoAreaEnemErrorCodes.BaseLegalCaractereInvalido);
}
