namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.PesoAreaEnemDeTeste;

public sealed class PesoAreaEnemTests
{
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
}
