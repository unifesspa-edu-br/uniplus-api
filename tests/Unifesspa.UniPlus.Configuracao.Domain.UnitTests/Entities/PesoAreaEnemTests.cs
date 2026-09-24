namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PesoAreaEnemTests
{
    private const string Resolucao = "Res. 805/2024";
    private const string Grupo = GrupoCurso.Tecnologica;
    private const string BaseLegal = "Res. 805/2024 Anexo I";

    private static List<AreaInformada> AreasValidas() =>
    [
        new(PesoAreaEnem.CodigoRedacao, 2.00m, 400m),
        new(PesoAreaEnem.CodigoCienciasDaNatureza, 1.50m, null),
        new(PesoAreaEnem.CodigoCienciasHumanas, 2.50m, null),
        new(PesoAreaEnem.CodigoLinguagens, 2.50m, null),
        new(PesoAreaEnem.CodigoMatematica, 1.50m, null),
    ];

    private static Result<PesoAreaEnem> Criar(
        IReadOnlyList<AreaInformada>? areas = null,
        string resolucao = Resolucao,
        string grupo = Grupo,
        string? baseLegal = BaseLegal) =>
        PesoAreaEnem.Criar(resolucao, grupo, areas ?? AreasValidas(), baseLegal);

    private static List<AreaInformada> ComArea(
        int indice, AreaInformada area)
    {
        List<AreaInformada> areas = AreasValidas();
        areas[indice] = area;
        return areas;
    }

    [Fact(DisplayName = "As cinco áreas têm o código e o rótulo oficial da Resolução nº 805/2024/Consepe, na ordem canônica")]
    public void Areas_CodigoERotuloNaOrdemCanonica()
    {
        PesoAreaEnem.Areas.Should().Equal(
            new AreaEnemDescrita("REDACAO", "Redação"),
            new AreaEnemDescrita("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias"),
            new AreaEnemDescrita("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias"),
            new AreaEnemDescrita("LINGUAGENS", "Linguagens e suas Tecnologias"),
            new AreaEnemDescrita("MATEMATICA", "Matemática e suas Tecnologias"));
    }

    [Fact(DisplayName = "Criar com dados válidos grava código, rótulo oficial, peso e corte de cada área")]
    public void Criar_DadosValidos_GravaAsAreas()
    {
        PesoAreaEnem peso = Criar().Value!;

        peso.Id.Should().NotBe(Guid.Empty);
        peso.Resolucao.Should().Be(Resolucao);
        peso.GrupoCurso.Codigo.Should().Be(Grupo);
        peso.BaseLegal.Should().Be(BaseLegal);
        peso.IsDeleted.Should().BeFalse();
        peso.AreasDaLinha.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().Equal(
            ("REDACAO", "Redação", 2.00m, (decimal?)400.000m),
            ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, (decimal?)null),
            ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, (decimal?)null),
            ("LINGUAGENS", "Linguagens e suas Tecnologias", 2.50m, (decimal?)null),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, (decimal?)null));
    }

    [Fact(DisplayName = "As áreas saem na ordem canônica mesmo quando chegam em outra ordem")]
    public void Criar_AreasForaDeOrdem_SaemNaOrdemCanonica()
    {
        List<AreaInformada> areas = AreasValidas();
        areas.Reverse();

        PesoAreaEnem peso = Criar(areas).Value!;

        peso.AreasDaLinha.Select(a => a.Codigo).Should().Equal(PesoAreaEnem.Areas.Select(a => a.Codigo));
    }

    [Fact(DisplayName = "O rótulo é sempre o do sistema: não há como gravar outro")]
    public void Criar_RotuloVemDoSistema()
    {
        PesoAreaEnem peso = Criar().Value!;

        peso.AreasDaLinha.Select(a => a.Rotulo).Should().Equal(PesoAreaEnem.Areas.Select(a => a.Rotulo));
    }

    [Fact(DisplayName = "Peso e corte são arredondados à escala persistida")]
    public void Criar_ArredondaPesoECorte()
    {
        PesoAreaEnem peso = Criar(ComArea(0, new(PesoAreaEnem.CodigoRedacao, 1.005m, 399.9995m))).Value!;

        PesoAreaEnemArea redacao = peso.AreasDaLinha[0];
        redacao.Peso.Should().Be(1.00m);
        redacao.Corte.Should().Be(400.000m);
    }

    [Fact(DisplayName = "Redação sem corte é aceita")]
    public void Criar_RedacaoSemCorte_Aceita()
    {
        PesoAreaEnem peso = Criar(ComArea(0, new(PesoAreaEnem.CodigoRedacao, 2m, null))).Value!;

        peso.AreasDaLinha[0].Corte.Should().BeNull();
    }

    [Theory(DisplayName = "Criar com resolução ausente ou em branco falha")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemResolucao_Falha(string resolucao)
    {
        Result<PesoAreaEnem> resultado = Criar(resolucao: resolucao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.ResolucaoObrigatoria);
    }

    [Theory(DisplayName = "Criar recusa resolução com caractere de controle, de formatação ou de quebra de linha")]
    [InlineData("805\u0000")]
    [InlineData("Res.\n805/2024")]
    [InlineData("Res. \u202E4202/508")]
    [InlineData("Res.\u2029805")]
    public void Criar_ResolucaoComCaractereInvisivel_Falha(string resolucao)
    {
        Result<PesoAreaEnem> resultado = Criar(resolucao: resolucao);

        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "resolucao" && e.Error.Code == PesoAreaEnemErrorCodes.ResolucaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar recusa resolução com surrogate sem par, sem exceção da normalização")]
    public void Criar_ResolucaoComSurrogateSemPar_Falha()
    {
        Result<PesoAreaEnem> resultado = Criar(resolucao: "Res." + (char)0xD800);

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == PesoAreaEnemErrorCodes.ResolucaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar grava a resolução aparada e em NFC")]
    public void Criar_GravaAResolucaoEmNfc()
    {
        string decomposta = "Resolução 805/2024".Normalize(System.Text.NormalizationForm.FormD);

        PesoAreaEnem peso = Criar(resolucao: $" {decomposta} ").Value!;

        peso.Resolucao.Should().Be("Resolução 805/2024".Normalize(System.Text.NormalizationForm.FormC),
            "o processo seletivo congela a resolução pelo valor, e o envelope canônico a emite em NFC");
    }

    [Fact(DisplayName = "Criar mede a base legal depois do NFC, que pode alongar o texto")]
    public void Criar_BaseLegalQueOnfcAlonga_Falha()
    {
        // U+0344 vira dois caracteres em NFC, e 'b' não se compõe com eles: 500 digitados, 501 gravados.
        string baseLegal = new string('b', 499) + "\u0344";
        baseLegal.Length.Should().Be(500, "pré-condição: o texto digitado cabe no limite");

        Result<PesoAreaEnem> resultado = Criar(baseLegal: baseLegal);

        resultado.Errors.Should().ContainSingle(e => e.Field == "baseLegal" && e.Error.Code == PesoAreaEnemErrorCodes.BaseLegalTamanho);
    }

    [Fact(DisplayName = "Criar e Atualizar gravam a base legal aparada e em NFC")]
    public void CriarEAtualizar_GravamABaseLegalEmNfc()
    {
        string composta = "Resolução nº 805/2024/Consepe".Normalize(System.Text.NormalizationForm.FormC);
        string decomposta = composta.Normalize(System.Text.NormalizationForm.FormD);

        PesoAreaEnem peso = Criar(baseLegal: $" {decomposta} ").Value!;
        peso.BaseLegal.Should().Be(composta);

        peso.Atualizar(AreasValidas(), $"{decomposta} – Anexo I").IsSuccess.Should().BeTrue();
        peso.BaseLegal.Should().Be($"{composta} – Anexo I");
    }

    [Fact(DisplayName = "Criar recusa pelo tamanho, antes de varrer o texto, a resolução maior que o teto")]
    public void Criar_ResolucaoMuitoGrande_RecusaPeloTamanhoAntesDeVarrer()
    {
        // A quebra de linha no fim daria recusa por caractere inválido se o texto fosse varrido.
        string enorme = new string('R', 1_000_000) + "\n1";

        Result<PesoAreaEnem> resultado = Criar(resolucao: enorme);

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == PesoAreaEnemErrorCodes.ResolucaoTamanho);
        PesoAreaEnem.NormalizarResolucao(enorme).Should().BeNull();
    }

    [Fact(DisplayName = "Criar aceita a resolução decomposta que, em NFC, cabe na coluna")]
    public void Criar_ResolucaoDecompostaQueCabeEmNfc_Aceita()
    {
        // U+1F86 decompõe em quatro caracteres: 160 digitados, 40 gravados.
        string decomposta = string.Concat(Enumerable.Repeat("\u03B1\u0313\u0342\u0345", 40));

        PesoAreaEnem peso = Criar(resolucao: decomposta).Value!;

        peso.Resolucao.Should().Be(new string('\u1F86', 40));
    }

    [Theory(DisplayName = "NormalizarResolucao devolve a forma gravada, ou nulo para o que não pode ser resolução gravada")]
    [InlineData(" Res. 805/2024 ", "Res. 805/2024")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("805\u0000", null)]
    [InlineData("Res.\n805", null)]
    [InlineData("Resolução com quarenta e um caracteres ..", null)]
    public void NormalizarResolucao_DevolveAFormaGravada(string? resolucao, string? esperada) =>
        PesoAreaEnem.NormalizarResolucao(resolucao).Should().Be(esperada);

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
        List<AreaInformada> areas = AreasValidas();
        areas[1] = new(PesoAreaEnem.CodigoCienciasDaNatureza, -1m, null);
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

    [Fact(DisplayName = "Atualizar muda peso, corte e base legal, sem mexer em código, rótulo, resolução e grupo")]
    public void Atualizar_MudaSoOsValores()
    {
        PesoAreaEnem peso = Criar().Value!;
        Guid id = peso.Id;
        List<AreaInformada> novas = AreasValidas();
        novas[0] = new(PesoAreaEnem.CodigoRedacao, 3m, null);
        novas[4] = new(PesoAreaEnem.CodigoMatematica, 4.25m, null);

        Result resultado = peso.Atualizar(novas, "  Nova base  ");

        resultado.IsSuccess.Should().BeTrue();
        peso.Id.Should().Be(id);
        peso.Resolucao.Should().Be(Resolucao);
        peso.GrupoCurso.Codigo.Should().Be(Grupo);
        peso.BaseLegal.Should().Be("Nova base");
        peso.AreasDaLinha[0].Peso.Should().Be(3m);
        peso.AreasDaLinha[0].Corte.Should().BeNull();
        peso.AreasDaLinha[4].Peso.Should().Be(4.25m);
        peso.AreasDaLinha.Select(a => (a.Codigo, a.Rotulo)).Should().Equal(
            PesoAreaEnem.Areas.Select(a => (a.Codigo, a.Rotulo)));
    }

    [Fact(DisplayName = "Atualizar mantém a mesma instância de cada área: muda os valores no lugar")]
    public void Atualizar_MudaNoLugar()
    {
        PesoAreaEnem peso = Criar().Value!;
        PesoAreaEnemArea[] antes = [.. peso.AreasDaLinha];

        peso.Atualizar(AreasValidas(), BaseLegal).IsSuccess.Should().BeTrue();

        peso.AreasDaLinha.Should().HaveCount(5);
        for (int i = 0; i < antes.Length; i++)
        {
            peso.AreasDaLinha[i].Should().BeSameAs(antes[i]);
        }
    }

    [Fact(DisplayName = "Atualizar com área inválida falha e não muda nada")]
    public void Atualizar_Invalido_NaoMuda()
    {
        PesoAreaEnem peso = Criar().Value!;

        Result resultado = peso.Atualizar(ComArea(1, new(PesoAreaEnem.CodigoCienciasDaNatureza, -1m, null)), "Outra base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Field.Should().Be("areas[1].peso");
        peso.AreasDaLinha[1].Peso.Should().Be(1.50m);
        peso.BaseLegal.Should().Be(BaseLegal);
    }

    [Fact(DisplayName = "ValidarCamposDoPayload recusa sem instância, com o mesmo campo da gravação")]
    public void ValidarCamposDoPayload_Recusa()
    {
        Result resultado = PesoAreaEnem.ValidarCamposDoPayload(
            ComArea(2, new(PesoAreaEnem.CodigoCienciasHumanas, 1m, 1500m)), BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Field.Should().Be("areas[2].corte");
    }

    [Fact(DisplayName = "ValidarCamposDoPayload aceita o payload válido")]
    public void ValidarCamposDoPayload_Aceita()
    {
        PesoAreaEnem.ValidarCamposDoPayload(AreasValidas(), BaseLegal).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Código da área com espaço em volta é aceito, como resolução e grupo")]
    public void Criar_CodigoComEspacos_Aceita()
    {
        PesoAreaEnem peso = Criar(ComArea(4, new("  MATEMATICA ", 1.50m, null))).Value!;

        peso.AreasDaLinha[4].Codigo.Should().Be(PesoAreaEnem.CodigoMatematica);
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
        mensagem.Should().Contain(new string('X', 40) + "…");
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

    [Fact(DisplayName = "O corte da mensagem não parte um par substituto na fronteira")]
    public void Criar_CodigoComEmojiNaFronteira_NaoPartePar()
    {
        string codigo = new string('X', 39) + "😀" + "RESTO";

        Result<PesoAreaEnem> resultado = Criar(ComArea(2, new(codigo, 1m, null)));

        string mensagem = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio).Error.Message;
        mensagem.Should().Contain(new string('X', 39) + "…");
        for (int i = 0; i < mensagem.Length; i++)
        {
            if (char.IsHighSurrogate(mensagem[i]))
            {
                (i + 1 < mensagem.Length && char.IsLowSurrogate(mensagem[i + 1])).Should().BeTrue("nenhum surrogate fica isolado");
            }
            else if (char.IsLowSurrogate(mensagem[i]))
            {
                (i > 0 && char.IsHighSurrogate(mensagem[i - 1])).Should().BeTrue("nenhum surrogate fica isolado");
            }
        }
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
        mensagem.Should().Contain("\"AB?CD?EF?GH?IJ\"");
        mensagem.Should().NotContain("\n").And.NotContain("\u202E").And.NotContain("\u2028").And.NotContain("\u2029");
    }

    [Fact(DisplayName = "Código recusado não volta com caractere de formatação fora do plano básico")]
    public void Criar_CodigoComTagForaDoPlanoBasico_NaoEcoaATag()
    {
        // U+E0041 é um caractere de formatação (tag) representado por um par substituto: a
        // classificação por unidade UTF-16 veria só as duas metades e o deixaria passar.
        Result<PesoAreaEnem> resultado = Criar(ComArea(2, new("AB\U000E0041CD", 1m, null)));

        string mensagem = resultado.Errors.Single(e => e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio).Error.Message;
        mensagem.Should().Contain("\"AB?CD\"");
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
