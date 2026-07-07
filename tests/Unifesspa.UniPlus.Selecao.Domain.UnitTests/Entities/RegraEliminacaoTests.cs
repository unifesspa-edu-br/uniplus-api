namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RegraEliminacaoTests
{
    private static ReferenciaRegra Regra(string codigo) =>
        ReferenciaRegra.Criar(codigo, "v1", new string('a', 64)).Value!;

    [Fact(DisplayName = "Criar ELIM-NOTA-MINIMA-ETAPA com args compatíveis tem sucesso")]
    public void Criar_NotaMinimaEtapa_Sucesso()
    {
        Guid etapaId = Guid.CreateVersion7();
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa), new ArgsElimNotaMinimaEtapa(etapaId, 4m));

        resultado.IsSuccess.Should().BeTrue();
        ((ArgsElimNotaMinimaEtapa)resultado.Value!.Args).EtapaRef.Should().Be(etapaId);
    }

    [Fact(DisplayName = "Criar ELIM-CORTE-REDACAO com mínimo válido tem sucesso")]
    public void Criar_CorteRedacao_Sucesso()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteRedacao), new ArgsElimCorteRedacao(400m));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar ELIM-ZERO-EM-AREA (sem args) tem sucesso")]
    public void Criar_ZeroEmArea_Sucesso()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimZeroEmArea), new ArgsElimZeroEmArea());

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com args incompatíveis com a regra falha")]
    public void Criar_ArgsIncompativeis_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa), new ArgsElimZeroEmArea());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.ArgsIncompativeisComRegra");
    }

    [Fact(DisplayName = "Criar ELIM-NOTA-MINIMA-ETAPA com nota mínima negativa falha")]
    public void Criar_NotaMinimaNegativa_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa), new ArgsElimNotaMinimaEtapa(Guid.CreateVersion7(), -1m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.NotaMinimaInvalida");
    }

    [Fact(DisplayName = "Criar ELIM-CORTE-REDACAO com mínimo negativo falha")]
    public void Criar_MinimoNegativo_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteRedacao), new ArgsElimCorteRedacao(-1m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.MinimoInvalido");
    }
}
