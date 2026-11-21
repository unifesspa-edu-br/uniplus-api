namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static ModeloDeFase;

public sealed class ItensDaFaseNoCronogramaTests
{
    private static BancaRequerida Banca(TipoDeBanca tipo, params string[] categorias) =>
        BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.De(tipo),
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

    [Fact(DisplayName = "Os candidatos atendidos ficam vermelhos quando duas bancas do mesmo código não se distinguem")]
    public void ItemDosCandidatosAtendidos_BancasIndistinguiveis_FicaVermelho()
    {
        FaseDeclarada indistinguiveis = new ModeloDeFase("ANALISE", 1) { Bancas = [Banca(TipoDeBanca.AnaliseDocumental), Banca(TipoDeBanca.AnaliseDocumental)] }.Declarar();
        FaseDeclarada distintas = new ModeloDeFase("ANALISE", 1) { Bancas = [Banca(TipoDeBanca.AnaliseDocumental, "RENDA"), Banca(TipoDeBanca.AnaliseDocumental, "ETNICO_RACIAL")] }.Declarar();

        Cronograma(indistinguiveis).ItemDosCandidatosAtendidos().Ok.Should().BeFalse();
        Cronograma(distintas).ItemDosCandidatosAtendidos().Should().Match<ItemConformidade>(
            static item => item.Ok && item.Codigo == "cronograma_candidatos_atendidos_das_bancas");
    }
}
