namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static ModeloDeFase;

public sealed class ItensDaFaseNoCronogramaTests
{
    private static BancaRequerida Banca(string codigo, params string[] categorias) =>
        BancaRequerida.Criar(
            Guid.CreateVersion7(), codigo, "Banca de teste",
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))]);

    [Fact(DisplayName = "A âncora do recurso na fase fica verde quando o prazo ancora num preliminar da própria fase")]
    public void ItemDaAncoraDoRecurso_AncoradoNoPreliminar_FicaVerde()
    {
        ProdutoDaFase preliminar = Preliminar("RESULTADO");
        FaseDeclarada fase = (new ModeloDeFase("RECURSO", 1) { RegraRecurso = RecursoAncoradoEm(preliminar.Id) }.Publicando(preliminar)).Declarar();

        ItemConformidade item = Cronograma(fase).ItemDaAncoraDoRecurso();

        item.Codigo.Should().Be("cronograma_ancora_do_recurso");
        item.Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "A âncora do recurso fica vermelha na fase sem preliminar e na âncora em produto que não é preliminar")]
    public void ItemDaAncoraDoRecurso_SemPreliminarOuAncoraForaDele_FicaVermelho()
    {
        ProdutoDaFase definitivo = Definitivo("RESULTADO");
        FaseDeclarada semPreliminar = new ModeloDeFase("RECURSO", 1) { RegraRecurso = RecursoAncoradoEm(Guid.CreateVersion7()) }.Publicando(definitivo).Declarar();
        FaseDeclarada ancoraErrada = new ModeloDeFase("RECURSO", 1) { RegraRecurso = RecursoAncoradoEm(definitivo.Id) }.Publicando(Preliminar("RESULTADO"), definitivo).Declarar();

        Cronograma(semPreliminar).ItemDaAncoraDoRecurso().Ok.Should().BeFalse();
        Cronograma(ancoraErrada).ItemDaAncoraDoRecurso().Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O recorte de competência fica vermelho quando duas bancas do mesmo código não se distinguem")]
    public void ItemDoRecorteDeCompetencia_BancasIndistinguiveis_FicaVermelho()
    {
        FaseDeclarada indistinguiveis = new ModeloDeFase("ANALISE", 1) { Bancas = [Banca("DOCUMENTAL"), Banca("DOCUMENTAL")] }.Declarar();
        FaseDeclarada distintas = new ModeloDeFase("ANALISE", 1) { Bancas = [Banca("DOCUMENTAL", "RENDA"), Banca("DOCUMENTAL", "ETNICO_RACIAL")] }.Declarar();

        Cronograma(indistinguiveis).ItemDoRecorteDeCompetencia().Ok.Should().BeFalse();
        Cronograma(distintas).ItemDoRecorteDeCompetencia().Should().Match<ItemConformidade>(
            static item => item.Ok && item.Codigo == "cronograma_recorte_de_competencia_das_bancas");
    }
}
