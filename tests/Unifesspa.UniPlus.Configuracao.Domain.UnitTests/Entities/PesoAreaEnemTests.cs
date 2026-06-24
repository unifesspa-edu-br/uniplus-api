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

    private static Result<PesoAreaEnem> Criar(
        string resolucao = Resolucao,
        string grupo = Grupo,
        decimal redacao = 1.50m,
        decimal cn = 1.00m,
        decimal ch = 1.00m,
        decimal lc = 1.00m,
        decimal mt = 2.00m,
        decimal? corte = 400m,
        string baseLegal = BaseLegal) =>
        PesoAreaEnem.Criar(resolucao, grupo, redacao, cn, ch, lc, mt, corte, baseLegal);

    [Fact(DisplayName = "Criar com dados válidos preenche os pesos e fica ativa com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        PesoAreaEnem peso = Criar().Value!;

        peso.Id.Should().NotBe(Guid.Empty);
        peso.Resolucao.Should().Be(Resolucao);
        peso.GrupoCurso.Valor.Should().Be(Grupo);
        peso.PesoRedacao.Should().Be(1.50m);
        peso.PesoCienciasNatureza.Should().Be(1.00m);
        peso.PesoCienciasHumanas.Should().Be(1.00m);
        peso.PesoLinguagens.Should().Be(1.00m);
        peso.PesoMatematica.Should().Be(2.00m);
        peso.CorteRedacao.Should().Be(400m);
        peso.BaseLegal.Should().Be(BaseLegal);
        peso.IsDeleted.Should().BeFalse();
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

    [Theory(DisplayName = "Criar com grupo fora do domínio falha")]
    [InlineData("Engenharias")]
    [InlineData("Humanística III")]
    public void Criar_GrupoInvalido_Falha(string grupo)
    {
        Result<PesoAreaEnem> resultado = Criar(grupo: grupo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.GrupoCursoInvalido);
    }

    [Theory(DisplayName = "Criar com peso negativo em qualquer das cinco áreas falha")]
    [InlineData(-1.00, 1, 1, 1, 1)]
    [InlineData(1, -1.00, 1, 1, 1)]
    [InlineData(1, 1, -1.00, 1, 1)]
    [InlineData(1, 1, 1, -1.00, 1)]
    [InlineData(1, 1, 1, 1, -1.00)]
    public void Criar_PesoNegativo_Falha(decimal redacao, decimal cn, decimal ch, decimal lc, decimal mt)
    {
        Result<PesoAreaEnem> resultado = Criar(redacao: redacao, cn: cn, ch: ch, lc: lc, mt: mt);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.PesoNegativo);
    }

    [Fact(DisplayName = "Criar com peso zero é aceito")]
    public void Criar_PesoZero_Aceita()
    {
        Result<PesoAreaEnem> resultado = Criar(ch: 0.00m);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.PesoCienciasHumanas.Should().Be(0.00m);
    }

    [Fact(DisplayName = "Criar sem informar o corte de redação assume o padrão 400")]
    public void Criar_SemCorte_AssumePadrao400()
    {
        PesoAreaEnem peso = Criar(corte: null).Value!;

        peso.CorteRedacao.Should().Be(400m);
    }

    [Fact(DisplayName = "Criar com corte de redação informado persiste o valor")]
    public void Criar_ComCorteInformado_Persiste()
    {
        PesoAreaEnem peso = Criar(corte: 500.000m).Value!;

        peso.CorteRedacao.Should().Be(500.000m);
    }

    [Fact(DisplayName = "Criar com corte de redação negativo falha")]
    public void Criar_CorteNegativo_Falha()
    {
        Result<PesoAreaEnem> resultado = Criar(corte: -10.000m);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.CorteRedacaoNegativo);
    }

    [Theory(DisplayName = "Criar sem base legal falha")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemBaseLegal_Falha(string baseLegal)
    {
        Result<PesoAreaEnem> resultado = Criar(baseLegal: baseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.BaseLegalObrigatoria);
    }

    [Fact(DisplayName = "Atualizar aplica novos pesos e preserva Id, resolução e grupo (CA-04b)")]
    public void Atualizar_DadosValidos_PreservaChaveEId()
    {
        PesoAreaEnem peso = Criar().Value!;
        Guid idOriginal = peso.Id;

        Result resultado = peso.Atualizar(2.00m, 1.50m, 1.50m, 1.50m, 3.00m, 450.000m, "Res. 805/2024 Anexo I (revisado)");

        resultado.IsSuccess.Should().BeTrue();
        peso.Id.Should().Be(idOriginal);
        peso.Resolucao.Should().Be(Resolucao);
        peso.GrupoCurso.Valor.Should().Be(Grupo);
        peso.PesoRedacao.Should().Be(2.00m);
        peso.CorteRedacao.Should().Be(450.000m);
    }

    [Fact(DisplayName = "Atualizar com peso negativo falha e mantém a linha inalterada")]
    public void Atualizar_PesoNegativo_Falha()
    {
        PesoAreaEnem peso = Criar().Value!;

        Result resultado = peso.Atualizar(1.50m, 1.00m, 1.00m, 1.00m, -1.00m, 400m, BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.PesoNegativo);
        peso.PesoMatematica.Should().Be(2.00m);
    }

    [Fact(DisplayName = "Atualizar com corte negativo falha")]
    public void Atualizar_CorteNegativo_Falha()
    {
        PesoAreaEnem peso = Criar().Value!;

        Result resultado = peso.Atualizar(1.50m, 1.00m, 1.00m, 1.00m, 2.00m, -1.000m, BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.CorteRedacaoNegativo);
    }

    [Fact(DisplayName = "Atualizar esvaziando a base legal falha")]
    public void Atualizar_BaseLegalVazia_Falha()
    {
        PesoAreaEnem peso = Criar().Value!;

        Result resultado = peso.Atualizar(1.50m, 1.00m, 1.00m, 1.00m, 2.00m, 400m, "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.BaseLegalObrigatoria);
    }
}
