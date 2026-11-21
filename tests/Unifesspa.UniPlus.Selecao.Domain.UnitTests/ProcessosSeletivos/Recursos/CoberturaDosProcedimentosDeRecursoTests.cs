namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos.ProcedimentosDeRecursoParaTestes;

/// <summary>Quais tipos de decisão que o lugar declara ficam sem procedimento: sem alvo cobre o tipo, com alvo só cobre o universo inteiro.</summary>
public sealed class CoberturaDosProcedimentosDeRecursoTests
{
    private static IReadOnlyList<TipoMotivoDeDecisao> SemProcedimento(ContextoDosProcedimentosDeRecurso contexto, params ProcedimentoDeRecursoDeclarado[] declarados) =>
        CoberturaDosProcedimentosDeRecurso.TiposSemProcedimento(ProcedimentosDeRecurso.De(declarados), contexto);

    [Fact(DisplayName = "Tipo que o lugar declara e nenhum procedimento cobre fica sem procedimento")]
    public void TipoSemDeclaracao_FicaSemProcedimento() =>
        SemProcedimento(Contexto()).Should().Equal(TipoMotivoDeDecisao.Classificacao);

    [Fact(DisplayName = "Lugar que não declara tipo de decisão não exige procedimento")]
    public void LugarSemTipos_NaoExige() =>
        SemProcedimento(Contexto(tiposDoLugar: [])).Should().BeEmpty();

    [Fact(DisplayName = "Lugar cuja declaração dos tipos foi recusada não tem tipo a cobrir")]
    public void TiposDoLugarRecusados_NaoExige() =>
        SemProcedimento(Contexto() with { TiposDeDecisaoDoLugar = null }).Should().BeEmpty();

    [Fact(DisplayName = "Procedimento sem alvo cobre o tipo inteiro")]
    public void SemAlvo_CobreOTipo() =>
        SemProcedimento(Contexto(), Procedimento()).Should().BeEmpty();

    [Fact(DisplayName = "Procedimento só para parte dos itens da etapa não cobre o tipo")]
    public void ParteDosItens_NaoCobre() =>
        SemProcedimento(Contexto(itens: [Item, OutroItem]), Procedimento(alvo: AlvoItem(Item)))
            .Should().Equal(TipoMotivoDeDecisao.Classificacao);

    [Fact(DisplayName = "Procedimentos para todos os itens da etapa cobrem o tipo")]
    public void TodosOsItens_Cobrem() =>
        SemProcedimento(
            Contexto(itens: [Item, OutroItem]),
            Procedimento(alvo: AlvoItem(Item)),
            Procedimento(alvo: AlvoItem(OutroItem))).Should().BeEmpty();

    [Fact(DisplayName = "Procedimentos para todas as áreas do ENEM cobrem o tipo; faltando uma, não")]
    public void Areas_CobremSoQuandoCompletas()
    {
        ProcedimentoDeRecursoDeclarado[] todas = [.. AreaDoEnem.Todas.Select(area => Procedimento(alvo: AlvoArea(area)))];

        SemProcedimento(Contexto(processoTemNotaDoEnem: true), todas).Should().BeEmpty();
        SemProcedimento(Contexto(processoTemNotaDoEnem: true), todas[1..]).Should().Equal(TipoMotivoDeDecisao.Classificacao);
    }

    [Fact(DisplayName = "Cada tipo é conferido à parte: o procedimento de um tipo não cobre o outro")]
    public void CadaTipo_ConferidoAParte() =>
        SemProcedimento(
            Contexto(tiposDoLugar: [TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.AnaliseDocumental]),
            Procedimento(TipoMotivoDeDecisao.AnaliseDocumental)).Should().Equal(TipoMotivoDeDecisao.Classificacao);
}
