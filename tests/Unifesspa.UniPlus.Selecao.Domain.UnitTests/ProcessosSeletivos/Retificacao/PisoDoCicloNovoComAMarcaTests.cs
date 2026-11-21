namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao.FaseDaRetificacaoDeTeste;

/// <summary>
/// O piso do ciclo novo com a marca "no mesmo momento da anterior": o momento da fase é o conjunto das fases mantidas que vêm antes
/// dela, e o preliminar entra no piso quando a fase dele, ou uma que vem antes, foi alterada, mudou de marca ou mudou de momento.
/// </summary>
public sealed class PisoDoCicloNovoComAMarcaTests
{
    private const string Materia = "RESULTADO_PROVA";
    private static readonly DateTimeOffset UmDiaDepois = Instante.AddDays(1);

    private static IdentidadeDoItemDoEdital[] Piso(FaseCronograma[] daBase, FaseCronograma[] novas) =>
        [.. PisoDoCicloNovo.Preliminares(daBase, [], novas, [], PreliminaresDe(daBase))];

    private static HashSet<IdentidadeDoItemDoEdital> PreliminaresDe(IEnumerable<FaseCronograma> fases) =>
        [.. fases.SelectMany(static fase => fase.Produtos.Select(produto => Item(fase, produto)))];

    private static FaseCronograma ComPreliminar(int ordem, string codigo, bool marcada = false) =>
        Fase(ordem, codigo, noMesmoMomentoDaAnterior: marcada, produtos: Preliminar($"{Materia}_{codigo}"));

    private static IdentidadeDoItemDoEdital ItemDe(FaseCronograma fase) => Item(fase, fase.Produtos.Single());

    [Fact(DisplayName = "A fase no mesmo momento da alterada não é atingida, e a posterior a elas é")]
    public void FaseNoMesmoMomentoDaAlterada_NaoEAtingida_EAPosteriorE()
    {
        FaseCronograma isencao = ComPreliminar(1, "ISENCAO");
        FaseCronograma inscricao = ComPreliminar(2, "INSCRICAO", marcada: true);
        FaseCronograma resultado = ComPreliminar(3, "RESULTADO");

        IdentidadeDoItemDoEdital[] piso = Piso([isencao, inscricao, resultado], [ComJanela(isencao, UmDiaDepois, UmDiaDepois.AddDays(8)), inscricao, resultado]);

        piso.Should().BeEquivalentTo([ItemDe(isencao), ItemDe(resultado)]);
    }

    [Fact(DisplayName = "Trocar só a marca de uma fase faz dela e das posteriores o piso, sem mudar campo nenhum")]
    public void MarcaTrocada_AtingeAFaseEAsPosteriores()
    {
        FaseCronograma isencao = ComPreliminar(1, "ISENCAO");
        FaseCronograma inscricao = ComPreliminar(2, "INSCRICAO");
        FaseCronograma resultado = ComPreliminar(3, "RESULTADO");

        IdentidadeDoItemDoEdital[] piso = Piso([isencao, inscricao, resultado], [isencao, ComMarca(inscricao, true), resultado]);

        piso.Should().BeEquivalentTo([ItemDe(inscricao), ItemDe(resultado)]);
    }

    [Fact(DisplayName = "Separar a fase de um grupo no mesmo momento atinge a fase separada e as posteriores, não as que ficam antes")]
    public void SepararFaseDoGrupo_AtingeASeparadaEAsPosteriores()
    {
        FaseCronograma primeira = ComPreliminar(1, "PRIMEIRA");
        FaseCronograma segunda = ComPreliminar(2, "SEGUNDA", marcada: true);
        FaseCronograma terceira = ComPreliminar(3, "TERCEIRA", marcada: true);
        FaseCronograma quarta = ComPreliminar(4, "QUARTA");

        IdentidadeDoItemDoEdital[] piso = Piso([primeira, segunda, terceira, quarta], [primeira, segunda, ComMarca(terceira, false), quarta]);

        piso.Should().BeEquivalentTo([ItemDe(terceira), ItemDe(quarta)]);
    }

    [Fact(DisplayName = "Trocar a ordem de duas fases muda o que vem antes de cada uma e atinge as duas")]
    public void OrdemTrocada_AtingeAsDuasFases()
    {
        FaseCronograma primeira = ComPreliminar(1, "PRIMEIRA");
        FaseCronograma segunda = ComPreliminar(2, "SEGUNDA");
        FaseCronograma terceira = ComPreliminar(3, "TERCEIRA");

        IdentidadeDoItemDoEdital[] piso = Piso(
            [primeira, segunda, terceira], [Renomeada(primeira, "PRIMEIRA", 2, [.. primeira.Produtos]), Renomeada(segunda, "SEGUNDA", 1, [.. segunda.Produtos]), terceira]);

        piso.Should().BeEquivalentTo([ItemDe(primeira), ItemDe(segunda), ItemDe(terceira)]);
    }

    [Fact(DisplayName = "Entrar uma fase no começo muda os números dos momentos, mas não o que vem antes de cada fase mantida")]
    public void FaseNovaNoComeco_NaoAtingeAsMantidas()
    {
        FaseCronograma primeira = ComPreliminar(1, "PRIMEIRA");
        FaseCronograma segunda = ComPreliminar(2, "SEGUNDA");
        FaseCronograma nova = Fase(1, "NOVA");

        IdentidadeDoItemDoEdital[] piso = Piso(
            [primeira, segunda], [nova, Renomeada(primeira, "PRIMEIRA", 2, [.. primeira.Produtos]), Renomeada(segunda, "SEGUNDA", 3, [.. segunda.Produtos])]);

        piso.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase que sai do cronograma novo não conta no piso")]
    public void FaseQueSai_NaoContaNoPiso()
    {
        FaseCronograma primeira = ComPreliminar(1, "PRIMEIRA");
        FaseCronograma segunda = ComPreliminar(2, "SEGUNDA");

        Piso([primeira, segunda], [primeira]).Should().BeEmpty();
    }
}
