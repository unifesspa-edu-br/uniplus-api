namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
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
                    Banca(TipoDeBanca.AnaliseDocumental, "RENDA"),
                    Banca(TipoDeBanca.Heteroidentificacao, "RENDA"),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A mesma categoria declarada duas vezes nos candidatos atendidos de uma banca é recusada")]
    public void CandidatosAtendidos_CategoriaRepetidaNaMesmaBanca_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Bancas = [Banca(TipoDeBanca.AnaliseDocumental, "RENDA", "RENDA")] });

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
                Banca(TipoDeBanca.AnaliseDocumental),
                Banca(TipoDeBanca.AnaliseDocumental),
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
                Banca(TipoDeBanca.AnaliseDocumental, "RENDA"),
                Banca(TipoDeBanca.AnaliseDocumental, "ETNICO_RACIAL"),
            ]);

        fase.BancasRequeridas.SelectMany(b => b.CandidatosAtendidos).Select(c => c.Codigo)
            .Should().BeEquivalentTo(["RENDA", "ETNICO_RACIAL"]);
    }

    [Fact(DisplayName = "A fase numera as bancas dentro de cada tipo na ordem em que as recebe")]
    public void Reidratar_BancasDeVariosTipos_NumeraDentroDeCadaTipo()
    {
        BancaRequerida analiseA = Banca(TipoDeBanca.AnaliseDocumental, "RENDA");
        BancaRequerida entrevista = Banca(TipoDeBanca.Entrevista);
        BancaRequerida analiseB = Banca(TipoDeBanca.AnaliseDocumental, "PCD");

        FaseCronograma fase = Reidratar([], null, [analiseA, entrevista, analiseB]);

        fase.BancasRequeridas.Select(static b => (b.Tipo.Codigo, b.PosicaoNoTipo)).Should().BeEquivalentTo(
            [(TipoDeBanca.AnaliseDocumental, 1), (TipoDeBanca.Entrevista, 1), (TipoDeBanca.AnaliseDocumental, 2)]);
    }

    [Fact(DisplayName = "A banca reconstruída dos dados salvos guarda o Id congelado e a fase não o troca")]
    public void ReconstruirDosDadosSalvos_PreservaOIdNaFase()
    {
        Guid id = Guid.CreateVersion7();
        BancaRequerida banca = BancaRequerida.ReconstruirDosDadosSalvos(id, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista, Guid.CreateVersion7()), []);

        FaseCronograma fase = Reidratar([], null, [banca]);

        fase.BancasRequeridas.Single().Id.Should().Be(id);
    }

    [Fact(DisplayName = "A banca da fase reconstruída sem o Id congelado é erro de programação")]
    public void ReconstruirDosDadosSalvos_IdVazio_Lanca()
    {
        Action reconstruir = () => BancaRequerida.ReconstruirDosDadosSalvos(Guid.Empty, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista, Guid.CreateVersion7()), []);

        reconstruir.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Duas bancas declaradas com o mesmo id de banca gravada são recusadas na segunda, junto com as demais recusas da fase")]
    public void Criar_BancasComOMesmoIdDeBancaGravada_RecusaASegundaEAcumula()
    {
        Guid idDaGravada = Guid.CreateVersion7();
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas =
            [
                FaseDeTesteDaAtualizacao.BancaQueSubstitui(idDaGravada, Guid.CreateVersion7(), TipoDeBanca.AnaliseDocumental, "RENDA"),
                FaseDeTesteDaAtualizacao.BancaQueSubstitui(idDaGravada, Guid.CreateVersion7(), TipoDeBanca.Heteroidentificacao, "PCD"),
            ],
            EmiteParecerIndividual = true,
        });

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(("bancasRequeridas[1].id", "Banca.IdDuplicado"));
        resultado.Errors.Should().HaveCountGreaterThan(1, "a recusa do id acumula com a do parecer sem resultado");
    }

    [Fact(DisplayName = "Bancas declaradas com ids de banca gravada diferentes, ou sem id, não são recusadas por id")]
    public void Criar_BancasComIdsDiferentesOuSemId_NaoRecusaPorId()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas =
            [
                FaseDeTesteDaAtualizacao.BancaQueSubstitui(Guid.CreateVersion7(), Guid.CreateVersion7(), TipoDeBanca.AnaliseDocumental, "RENDA"),
                FaseDeTesteDaAtualizacao.BancaQueSubstitui(Guid.CreateVersion7(), Guid.CreateVersion7(), TipoDeBanca.AnaliseDocumental, "PCD"),
                Banca(TipoDeBanca.Heteroidentificacao, "PPI"),
            ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
