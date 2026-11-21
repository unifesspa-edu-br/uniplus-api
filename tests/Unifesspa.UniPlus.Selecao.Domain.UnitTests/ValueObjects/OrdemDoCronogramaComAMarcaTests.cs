namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O momento de cada fase quando há a marca "no mesmo momento da anterior": a menor ordem é o momento 1, cada ordem seguinte é o
/// momento seguinte, e a ordem com fase marcada acontece no momento da ordem que vem antes dela.
/// </summary>
public sealed class OrdemDoCronogramaComAMarcaTests
{
    [Fact(DisplayName = "A fase marcada acontece no mesmo momento da anterior: nenhuma é posterior à outra")]
    public void Posterior_FaseMarcada_NaoEPosteriorAAnterior()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO");
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", noMesmoMomentoDaAnterior: true);
        OrdemDoCronograma ordem = OrdemDoCronograma.Do([isencao, inscricao]);

        ordem.Posterior(inscricao, isencao).Should().BeFalse();
        ordem.Posterior(isencao, inscricao).Should().BeFalse();
    }

    [Fact(DisplayName = "A fase seguinte à marcada é posterior às duas que acontecem juntas")]
    public void Posterior_FaseDepoisDaMarcada_EPosteriorAsDuas()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO");
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", noMesmoMomentoDaAnterior: true);
        FaseCronograma resultado = FaseDeTeste.Criar(3, "RESULTADO");
        OrdemDoCronograma ordem = OrdemDoCronograma.Do([isencao, inscricao, resultado]);

        ordem.Posterior(resultado, isencao).Should().BeTrue();
        ordem.Posterior(resultado, inscricao).Should().BeTrue();
    }

    [Fact(DisplayName = "A marca na primeira fase não tem efeito")]
    public void Posterior_MarcaNaPrimeiraFase_NaoTemEfeito()
    {
        FaseCronograma primeira = FaseDeTeste.Criar(1, "PRIMEIRA", noMesmoMomentoDaAnterior: true);
        FaseCronograma segunda = FaseDeTeste.Criar(2, "SEGUNDA");

        OrdemDoCronograma.Do([primeira, segunda]).Posterior(segunda, primeira).Should().BeTrue();
    }

    [Fact(DisplayName = "A ordem em que as fases chegam não muda os momentos")]
    public void Posterior_FasesForaDeOrdem_UsamOMomentoDaOrdemDeclarada()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO");
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", noMesmoMomentoDaAnterior: true);
        FaseCronograma resultado = FaseDeTeste.Criar(3, "RESULTADO");
        OrdemDoCronograma ordem = OrdemDoCronograma.Do([resultado, inscricao, isencao]);

        ordem.Posterior(inscricao, isencao).Should().BeFalse();
        ordem.Posterior(resultado, inscricao).Should().BeTrue();
    }

    [Fact(DisplayName = "No empate de momento, a mais tardia é a primeira na ordem recebida")]
    public void MaisTardia_FasesNoMesmoMomento_FicaComAPrimeiraRecebida()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO");
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", noMesmoMomentoDaAnterior: true);
        OrdemDoCronograma ordem = OrdemDoCronograma.Do([isencao, inscricao]);

        ordem.MaisTardia([isencao, inscricao]).Should().BeSameAs(isencao);
        ordem.MaisTardia([inscricao, isencao]).Should().BeSameAs(inscricao);
    }

    [Fact(DisplayName = "Cadeia de fases marcadas fica toda no momento da primeira, e a lacuna de ordem não cria momento vazio")]
    public void MomentosPorOrdem_CadeiaELacuna()
    {
        IReadOnlyDictionary<int, int> momentos = OrdemDoCronograma.MomentosPorOrdem(
            [(1, false), (2, true), (3, true), (7, false), (9, true), (12, false)]);

        momentos.Should().Equal(new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 1, [7] = 2, [9] = 2, [12] = 3 });
    }

    [Fact(DisplayName = "A marca na menor ordem não tem efeito, e uma ordem com alguma fase marcada fica no momento da anterior")]
    public void MomentosPorOrdem_MarcaNaMenorOrdemENaOrdemCompartilhada()
    {
        IReadOnlyDictionary<int, int> momentos = OrdemDoCronograma.MomentosPorOrdem([(5, true), (6, false), (6, true), (8, false)]);

        momentos.Should().Equal(new Dictionary<int, int> { [5] = 1, [6] = 1, [8] = 2 });
    }

    [Fact(DisplayName = "Sem a lista de fases, os momentos não se calculam")]
    public void MomentosPorOrdem_SemLista_Lanca()
    {
        Func<IReadOnlyDictionary<int, int>> calcular = () => OrdemDoCronograma.MomentosPorOrdem(null!);

        calcular.Should().Throw<ArgumentNullException>();
    }
}
