namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class FaseCronogramaTests
{
    [Fact(DisplayName = "Bancas de TIPOS distintos com o mesmo recorte são aceitas — o tipo já as distingue")]
    public void RecorteDeCompetencia_TiposDistintosMesmoRecorte_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA"),
                Banca("BANCA_HETEROIDENTIFICACAO", "RENDA"),
            ]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A mesma categoria declarada duas vezes no recorte de uma banca é recusada")]
    public void RecorteDeCompetencia_CategoriaRepetidaNaMesmaBanca_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            bancasRequeridas: [Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA", "RENDA")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Error.Code == "BancaRequerida.CategoriaDuplicadaNoRecorte"
                && e.Field == "bancasRequeridas[0].categoriasDocumentoIds");
    }

    [Fact(DisplayName = "Reidratar um envelope com duas bancas do mesmo tipo sem recorte lança — o estado não é escrevível")]
    public void Reidratar_MesmoTipoSemRecorte_Lanca()
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

    [Fact(DisplayName = "Reidratar com bancas do mesmo tipo e recortes distintos repõe as duas")]
    public void Reidratar_MesmoTipoRecortesDistintos_Repoe()
    {
        FaseCronograma fase = Reidratar(
            produtos: [],
            regraRecurso: null,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA"),
                Banca("BANCA_ANALISE_DOCUMENTAL", "ETNICO_RACIAL"),
            ]);

        fase.BancasRequeridas.SelectMany(b => b.RecorteDeCompetencia).Select(c => c.Codigo)
            .Should().BeEquivalentTo(["RENDA", "ETNICO_RACIAL"]);
    }
}
