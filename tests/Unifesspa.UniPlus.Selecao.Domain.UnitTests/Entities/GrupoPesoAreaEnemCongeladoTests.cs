namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class GrupoPesoAreaEnemCongeladoTests
{
    private const string BaseLegal = "Resolução nº 805/2024/Consepe – Anexo I";

    private static (string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)[] Areas() =>
    [
        ("REDACAO", "Redação", 2.00m, 400m),
        ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, null),
    ];

    [Fact(DisplayName = "Congela grupo, base legal e áreas, aparados e em NFC, vinculando cada área ao grupo")]
    public void Criar_ValoresValidos_CongelaNormalizado()
    {
        string rotuloDecomposto = "Saúde e Biológicas".Normalize(NormalizationForm.FormD);

        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            " SAUDE_E_BIOLOGICAS ", rotuloDecomposto, $" {BaseLegal} ", Areas());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        GrupoPesoAreaEnemCongelado grupo = resultado.Value!;
        grupo.GrupoAreaEnem.Codigo.Should().Be("SAUDE_E_BIOLOGICAS");
        grupo.GrupoAreaEnem.Rotulo.Should().Be("Saúde e Biológicas".Normalize(NormalizationForm.FormC));
        grupo.BaseLegal.Should().Be(BaseLegal);
        grupo.Areas.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().Equal(
            ("REDACAO", "Redação", 2.00m, (decimal?)400m),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, (decimal?)null));
        grupo.Areas.Should().OnlyContain(a => a.GrupoPesoAreaEnemCongeladoId == grupo.Id);
    }

    [Fact(DisplayName = "Grupo sem área é recusado")]
    public void Criar_SemAreas_Recusa()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar("TECNOLOGICA", "Tecnológica", BaseLegal, []);

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "GrupoPesoAreaEnemCongelado.SemAreas");
    }

    [Fact(DisplayName = "Área repetida no mesmo grupo é recusada")]
    public void Criar_AreaRepetida_Recusa()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", BaseLegal,
            [("REDACAO", "Redação", 2.00m, 400m), ("REDACAO", "Redação", 1.00m, null)]);

        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("areas[1]");
        erro.Error.Code.Should().Be("GrupoPesoAreaEnemCongelado.AreaRepetida");
    }

    [Theory(DisplayName = "Corte fora de 0 a 1000 é recusado no campo do corte")]
    [InlineData(-0.0001)]
    [InlineData(1000.0001)]
    public void Criar_CorteForaDaFaixa_Recusa(double corte)
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", BaseLegal, [("REDACAO", "Redação", 2.00m, (decimal)corte)]);

        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "areas[0].corte" && e.Error.Code == "GrupoPesoAreaEnemCongelado.CorteForaDaFaixa");
    }

    [Fact(DisplayName = "Corte nos limites da faixa é aceito")]
    public void Criar_CorteNosLimites_Aceita()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", BaseLegal,
            [("REDACAO", "Redação", 2.00m, 0m), ("MATEMATICA", "Matemática e suas Tecnologias", 0m, 1000m)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "ADR-0125: grupo, base legal, código de área e peso inválidos acumulam no mesmo lote")]
    public void Criar_VariasViolacoes_Acumula()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "   ", "Tecnológica", "   ",
            [(null, "Redação", 2.00m, null), ("MATEMATICA", "Matemática e suas Tecnologias", -1m, null)]);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ((string?)"grupoAreaEnem.codigo", "GrupoAreaEnemSnapshot.CodigoObrigatorio"),
            ((string?)"baseLegal", "GrupoPesoAreaEnemCongelado.BaseLegalObrigatoria"),
            ((string?)"areas[0].codigo", "GrupoPesoAreaEnemCongelado.AreaInvalida"),
            ((string?)"areas[1].peso", "GrupoPesoAreaEnemCongelado.PesoNegativo"),
        ]);
    }

    [Fact(DisplayName = "Grupo cujos pesos somam zero é recusado")]
    public void Criar_PesosSomamZero_Recusa()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", BaseLegal,
            [("REDACAO", "Redação", 0m, 400m), ("MATEMATICA", "Matemática e suas Tecnologias", 0m, null)]);

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Single();
        erro.Field.Should().Be("areas");
        erro.Error.Code.Should().Be("GrupoPesoAreaEnemCongelado.PesosSomamZero");
    }

    [Theory(DisplayName = "Base legal ou rótulo de área acima da coluna, ou com caractere nulo, são recusados")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Criar_TextoAcimaDaColunaOuComNulo_Recusa(bool baseLegalLonga, bool rotuloComNulo)
    {
        string baseLegal = baseLegalLonga ? new string('B', GrupoPesoAreaEnemCongelado.BaseLegalMaxLength + 1) : BaseLegal;
        string rotulo = rotuloComNulo ? "Reda\0ção" : "Redação";

        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", baseLegal, [("REDACAO", rotulo, 2.00m, null)]);

        string esperado = baseLegalLonga ? "GrupoPesoAreaEnemCongelado.BaseLegalInvalida" : "GrupoPesoAreaEnemCongelado.RotuloDaAreaInvalido";
        resultado.Errors.Should().ContainSingle(e => e.Error.Code == esperado);
    }

    [Fact(DisplayName = "Não-caractere no grupo, na base legal ou na área é recusado com erro nomeado, sem exceção")]
    public void Criar_NaoCaractere_RecusaSemExcecao()
    {
        string naoCaractere = ((char)0xFFFE).ToString();

        GrupoPesoAreaEnemCongelado.Criar("TECNOLOGICA", "Tecnológica" + naoCaractere, BaseLegal, Areas())
            .Errors.Should().ContainSingle(e => e.Error.Code == "GrupoAreaEnemSnapshot.CaractereInvalido");
        GrupoPesoAreaEnemCongelado.Criar("TECNOLOGICA", "Tecnológica", BaseLegal + naoCaractere, Areas())
            .Errors.Should().ContainSingle(e => e.Error.Code == "GrupoPesoAreaEnemCongelado.BaseLegalInvalida");
        GrupoPesoAreaEnemCongelado.Criar("TECNOLOGICA", "Tecnológica", BaseLegal, [("REDACAO", "Redação" + naoCaractere, 2m, null)])
            .Errors.Should().ContainSingle(e => e.Field == "areas[0].rotulo" && e.Error.Code == "GrupoPesoAreaEnemCongelado.RotuloDaAreaInvalido");
    }

    [Fact(DisplayName = "ADR-0125: área inválida ou repetida ainda tem o peso e o corte conferidos")]
    public void Criar_AreaInvalidaOuRepetida_AcumulaPesoECorte()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", BaseLegal,
            [(null, "Redação", -1m, null), ("MATEMATICA", "Matemática e suas Tecnologias", 1m, null), ("MATEMATICA", "Matemática e suas Tecnologias", 1m, 1001m)]);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ((string?)"areas[0].codigo", "GrupoPesoAreaEnemCongelado.AreaInvalida"),
            ((string?)"areas[0].peso", "GrupoPesoAreaEnemCongelado.PesoNegativo"),
            ((string?)"areas[2]", "GrupoPesoAreaEnemCongelado.AreaRepetida"),
            ((string?)"areas[2].corte", "GrupoPesoAreaEnemCongelado.CorteForaDaFaixa"),
        ]);
    }

    [Fact(DisplayName = "Código e rótulo da área inválidos voltam juntos, cada um no seu campo, sem repetir o valor recebido")]
    public void Criar_AreaComCodigoERotuloInvalidos_AcumulaSemRepetirOValor()
    {
        string codigoLongo = "CODIGO-FICTICIO-RECONHECIVEL-" + new string('X', GrupoPesoAreaEnemCongelado.AreaCodigoMaxLength);

        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", BaseLegal, [(codigoLongo, "ROTULO-FICTICIO-\0-RECONHECIVEL", 2m, null)]);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("areas[0].codigo", "GrupoPesoAreaEnemCongelado.AreaInvalida"),
            ("areas[0].rotulo", "GrupoPesoAreaEnemCongelado.RotuloDaAreaInvalido"));
        NenhumTextoRepeteOValorRecebido(resultado);
    }

    [Fact(DisplayName = "Código e rótulo do grupo inválidos voltam juntos, antes da base legal e das áreas, sem repetir o valor recebido")]
    public void Criar_GrupoComCodigoERotuloInvalidos_AcumulaNaOrdemSemRepetirOValor()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "GRUPO-FICTICIO-\0-RECONHECIVEL", "   ", "   ", []);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("grupoAreaEnem.codigo", "GrupoAreaEnemSnapshot.CaractereInvalido"),
            ("grupoAreaEnem.rotulo", "GrupoAreaEnemSnapshot.RotuloObrigatorio"),
            ("baseLegal", "GrupoPesoAreaEnemCongelado.BaseLegalObrigatoria"),
            ("areas", "GrupoPesoAreaEnemCongelado.SemAreas"));
        NenhumTextoRepeteOValorRecebido(resultado);
    }

    [Fact(DisplayName = "Área repetida é conferida pelo código mesmo com o rótulo inválido, e não repete o rótulo recebido")]
    public void Criar_AreaRepetidaComRotuloInvalido_RecusaOCodigoERotuloSemRepetirOValor()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", BaseLegal,
            [("REDACAO", "Redação", 2m, null), ("REDACAO", "ROTULO-FICTICIO-\0-RECONHECIVEL", 1m, null)]);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("areas[1]", "GrupoPesoAreaEnemCongelado.AreaRepetida"),
            ("areas[1].rotulo", "GrupoPesoAreaEnemCongelado.RotuloDaAreaInvalido"));
        NenhumTextoRepeteOValorRecebido(resultado);
    }

    [Fact(DisplayName = "Área repetida, peso negativo e corte fora da faixa não citam o rótulo recebido")]
    public void Criar_AreaRepetidaPesoNegativoECorteForaDaFaixa_NaoCitamORotulo()
    {
        Result<GrupoPesoAreaEnemCongelado> resultado = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", BaseLegal,
            [("REDACAO", "ÁREA FICTÍCIA RECONHECÍVEL", 2m, null), ("REDACAO", "ÁREA FICTÍCIA RECONHECÍVEL", -1m, 1001m)]);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("areas[1]", "GrupoPesoAreaEnemCongelado.AreaRepetida"),
            ("areas[1].peso", "GrupoPesoAreaEnemCongelado.PesoNegativo"),
            ("areas[1].corte", "GrupoPesoAreaEnemCongelado.CorteForaDaFaixa"));
        NenhumTextoRepeteOValorRecebido(resultado);
    }

    private static void NenhumTextoRepeteOValorRecebido(Result<GrupoPesoAreaEnemCongelado> resultado) =>
        resultado.Errors.SelectMany(e => new[] { e.Error.Message, e.Field })
            .Should().OnlyContain(texto => !texto!.Contains("FICTICIO", StringComparison.OrdinalIgnoreCase)
                && !texto.Contains("FICTÍCIA", StringComparison.OrdinalIgnoreCase)
                && !texto.Contains("RECONHEC", StringComparison.OrdinalIgnoreCase));
}
