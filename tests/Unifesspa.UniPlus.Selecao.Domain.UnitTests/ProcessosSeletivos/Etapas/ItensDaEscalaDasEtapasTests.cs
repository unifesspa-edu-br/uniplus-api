namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static ItensDeEtapaDeTeste;

public sealed class ItensDaEscalaDasEtapasTests
{
    [Fact(DisplayName = "Etapa de nota numérica sem nota máxima reprova o item da nota máxima, e com ela aprova")]
    public void NotaNumerica_SemNotaMaxima_Reprova()
    {
        EtapaDeclarada sem = (new ModeloDeEtapa("Prova") with { NotaMaxima = null }).Declarar();

        Item("etapa_com_nota_sem_nota_maxima", sem).Ok.Should().BeFalse();
        Item("etapa_com_nota_sem_nota_maxima", sem).Mensagem.Should().EndWith("(etapas: \"Prova\")");
        Item("etapa_com_nota_sem_nota_maxima", new ModeloDeEtapa("Prova").Declarar()).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Etapa sem nota nem conceito com nota máxima ou passo reprova o item da escala")]
    public void SemNota_ComEscala_Reprova()
    {
        EtapaDeclarada comNotaMaxima = (new ModeloDeEtapa("Triagem").SemNota() with { NotaMaxima = 10m }).Declarar();
        EtapaDeclarada comPasso = (new ModeloDeEtapa("Triagem").SemNota() with { Passo = 0.5m }).Declarar();

        Item("etapa_sem_nota_com_escala", comNotaMaxima).Ok.Should().BeFalse();
        Item("etapa_sem_nota_com_escala", comPasso).Ok.Should().BeFalse();
        Item("etapa_sem_nota_com_escala", new ModeloDeEtapa("Triagem").SemNota().Declarar()).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Etapa de nota do ENEM com nota máxima ou passo reprova o item do ENEM e nunca fica indeterminada")]
    public void NotaDoEnem_ComEscala_ReprovaSemIndeterminar()
    {
        EtapaDeclarada comEscala = (new ModeloDeEtapa("Nota do ENEM").DoEnem() with { NotaMaxima = 10m, Carater = (Unifesspa.UniPlus.Selecao.Domain.Enums.CaraterEtapa)99 }).Declarar();

        Item("etapa_do_enem_com_escala", comEscala).Mensagem.Should().EndWith("(etapas: \"Nota do ENEM\")");
        Item("etapa_do_enem_com_escala", new ModeloDeEtapa("Nota do ENEM").DoEnem().Declarar()).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa do ENEM só aparece no item do ENEM, para a mesma causa não render dois vermelhos")]
    public void NotaDoEnem_ComEscala_NaoApareceNosOutrosItens()
    {
        EtapaDeclarada comEscala = (new ModeloDeEtapa("Nota do ENEM").DoEnem() with { NotaMaxima = 10m }).Declarar();

        Vermelhos(null, comEscala).Should().Equal("etapa_do_enem_com_escala");
    }

    [Fact(DisplayName = "Nota mínima citada acima da nota máxima reprova e igual à nota máxima aprova")]
    public void NotaMinimaCitada_AcimaDaNotaMaxima_Reprova()
    {
        EtapaDeclarada etapa = new ModeloDeEtapa("Prova").Declarar();

        Item("nota_minima_da_regra_acima_da_nota_maxima", new CitacoesDeNota([], [(etapa.Id, 10.01m)]), etapa).Ok.Should().BeFalse();
        Item("nota_minima_da_regra_acima_da_nota_maxima", new CitacoesDeNota([], [(etapa.Id, 10m)]), etapa).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Sem nota máxima declarada a nota mínima citada não tem contra o que ser conferida")]
    public void NotaMinimaCitada_SemNotaMaxima_NaoConfere()
    {
        EtapaDeclarada etapa = (new ModeloDeEtapa("Prova") with { NotaMaxima = null }).Declarar();

        Item("nota_minima_da_regra_acima_da_nota_maxima", new CitacoesDeNota([], [(etapa.Id, 99m)]), etapa).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Etapa citada só pelo desempate é de nota numérica e deve ter nota máxima")]
    public void CitadaPeloDesempate_TemNota_ExigeNotaMaxima()
    {
        EtapaDeclarada etapa = (new ModeloDeEtapa("Triagem").SemNota() with { NotaMaxima = null }).Declarar();
        CitacoesDeNota citacoes = new([etapa.Id], []);

        Item("etapa_com_nota_sem_nota_maxima", citacoes, etapa).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "Itens de etapas reprovadas saem em ordem declarada")]
    public void Itens_EtapasReprovadas_SaemEmOrdemEstavel()
    {
        EtapaDeclarada tardia = (new ModeloDeEtapa("Zeta") with { NotaMaxima = null, Ordem = 2 }).Declarar();
        EtapaDeclarada precoce = (new ModeloDeEtapa("Beta") with { NotaMaxima = null, Ordem = 1 }).Declarar();
        EtapaDeclarada ultima = (new ModeloDeEtapa("Alfa") with { NotaMaxima = null, Ordem = 3 }).Declarar();

        Item("etapa_com_nota_sem_nota_maxima", tardia, ultima, precoce).Mensagem
            .Should().EndWith("(etapas: \"Beta\"; \"Zeta\"; \"Alfa\")");
    }
}
