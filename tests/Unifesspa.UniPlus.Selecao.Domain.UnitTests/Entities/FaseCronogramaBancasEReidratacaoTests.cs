namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioFaseCronograma;

public sealed class FaseCronogramaBancasEReidratacaoTests
{
    [Fact(DisplayName = "Bancas de TIPOS distintos com os mesmos candidatos atendidos são aceitas — o tipo já as distingue")]
    public void CandidatosAtendidos_TiposDistintosMesmosCandidatosAtendidos_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas = [
                    Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA"),
                    Banca("BANCA_HETEROIDENTIFICACAO", "RENDA"),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A mesma categoria declarada duas vezes nos candidatos atendidos de uma banca é recusada")]
    public void CandidatosAtendidos_CategoriaRepetidaNaMesmaBanca_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Bancas = [Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA", "RENDA")] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Error.Code == "BancaRequerida.CategoriaRepetidaNosCandidatosAtendidos"
                && e.Field == "bancasRequeridas[0].categoriasDocumentoIds");
    }

    [Fact(DisplayName = "Reidratar um envelope com duas bancas do mesmo tipo sem candidatos atendidos lança — o estado não é escrevível")]
    public void Reidratar_MesmoTipoSemCandidatosAtendidos_Lanca()
    {
        Action act = () => Reidratar(
            produtos: [],
            regraRecurso: null,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL"),
                Banca("BANCA_ANALISE_DOCUMENTAL"),
            ]);

        act.Should().Throw<ArgumentException>().WithParameterName("bancasRequeridas");
    }

    [Fact(DisplayName = "Reidratar com bancas do mesmo tipo e candidatos atendidos distintos repõe as duas")]
    public void Reidratar_MesmoTipoCandidatosAtendidosDistintos_Repoe()
    {
        FaseCronograma fase = Reidratar(
            produtos: [],
            regraRecurso: null,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA"),
                Banca("BANCA_ANALISE_DOCUMENTAL", "ETNICO_RACIAL"),
            ]);

        fase.BancasRequeridas.SelectMany(b => b.CandidatosAtendidos).Select(c => c.Codigo)
            .Should().BeEquivalentTo(["RENDA", "ETNICO_RACIAL"]);
    }
}
