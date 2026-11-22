namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Cobertura da redefinição do cronograma quando a fase muda de Ordem ou de identidade canônica:
/// a mesma fase na mesma posição é reconciliada (preserva o Id e a exigência), a troca de fase é
/// remoção mais inserção, e a permutação cíclica de Ordem é recusada.
/// </summary>
public sealed class ProcessoSeletivoReconciliacaoDeFasesPorOrdemTests
{
    [Fact(DisplayName = "Story #554/issue #893 (PR #900): redefinir o cronograma preservando a Ordem de uma fase referenciada por exigência viva é aceito — reconciliação, não guard bruto")]
    public void DefinirCronogramaFases_MesmaOrdemComExigenciaViva_Aceita()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        DocumentoExigido exigencia = Exigencia(fase.Id);
        ExigirDocumentos(processo, exigencia);

        // Mesma Ordem E mesma FaseCanonicaOrigemId: é a edição da mesma fase (uma edição que
        // só ajusta datas), não a troca de qual fase ocupa o slot. A reconciliação reusa a instância viva.
        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "INSCRICAO_REVISADA", fase.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle(f => f.Id == fase.Id, "a reconciliação por Ordem reusa a MESMA instância, preservando o Id");
        processo.CronogramaFases.Single().Codigo.Should().Be("INSCRICAO_REVISADA");
        exigencia.ExigidoNaFaseId.Should().Be(fase.Id, "a exigência nunca deixou de referenciar uma fase existente");
    }

    /// <summary>
    /// Fase diferente ocupando a mesma posição é remoção mais inserção, não edição: a
    /// exigência que era da fase antiga sai com ela. O que este teste protege é o silêncio —
    /// a exigência não pode sobreviver retargetada para a fase nova, que não a declarou.
    /// </summary>
    [Fact(DisplayName = "Trocar a fase canônica na MESMA Ordem leva a exigência da fase antiga junto — não a retargeta")]
    public void DefinirCronogramaFases_TrocaFaseCanonicaNaMesmaOrdemComExigenciaViva_LevaAExigenciaJunto()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        ExigirDocumentos(processo, Exigencia(fase.Id));

        // Mesma Ordem, mas FaseCanonicaOrigemId DIFERENTE (não informado — gera um novo): é uma
        // fase DIFERENTE ocupando o slot, não uma edição da mesma fase.
        Result resultado = processo.DefinirCronogramaFases([Fase(1, "ANALISE")], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty(
            "a exigência era da fase que saiu; sobreviver seria ficar pendurada numa fase que nunca a declarou");
        processo.RegrasDeComprovacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Trocar a fase canônica na MESMA Ordem, SEM exigência viva referenciando-a, é aceito como remoção+inserção — NÃO reusa a linha (o Id muda)")]
    public void DefinirCronogramaFases_TrocaFaseCanonicaNaMesmaOrdemSemExigenciaViva_AceitaComoRemocaoMaisInsercao()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);

        // Mesma Ordem, FaseCanonicaOrigemId DIFERENTE e nenhuma exigência na fase antiga: a reconciliação
        // casa por FaseCanonicaOrigemId, não por Ordem, então a linha antiga sai e uma nova entra
        // (nunca retargeta a origem de uma linha rastreada, o que criaria dependência circular).
        Result resultado = processo.DefinirCronogramaFases([Fase(1, "ANALISE")], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle(f => f.Id != fase.Id, "é uma fase canônica diferente — remoção+inserção, não reconciliação");
        processo.CronogramaFases.Single().Codigo.Should().Be("ANALISE");
    }

    [Fact(DisplayName = "Permutação cíclica de Ordem entre 2 fases retidas é recusada — sem ordem de UPDATE que resolva a troca")]
    public void DefinirCronogramaFases_PermutacaoCiclicaDeOrdemEntreDuasFases_Recusa()
    {
        FaseCronograma faseA = Fase(1, "A");
        FaseCronograma faseB = Fase(2, "B");
        ProcessoSeletivo processo = ProcessoComFases(faseA, faseB);

        // A assume a Ordem de B e B assume a Ordem de A — um ciclo fechado de tamanho 2.
        Result resultado = processo.DefinirCronogramaFases(
            [FaseNaOrdem(faseA, 2), FaseNaOrdem(faseB, 1)], []);

        resultado.IsFailure.Should().BeTrue("um ciclo fechado não tem uma ordem de UPDATE que o resolva num único SaveChanges");
        resultado.Error!.Code.Should().Be("FaseCronograma.PermutacaoDeOrdemNaoSuportada");
    }

    [Fact(DisplayName = "Permutação cíclica de Ordem entre 3 fases retidas é recusada")]
    public void DefinirCronogramaFases_PermutacaoCiclicaDeOrdemEntreTresFases_Recusa()
    {
        FaseCronograma faseA = Fase(1, "A");
        FaseCronograma faseB = Fase(2, "B");
        FaseCronograma faseC = Fase(3, "C");
        ProcessoSeletivo processo = ProcessoComFases(faseA, faseB, faseC);

        // A -> Ordem de B, B -> Ordem de C, C -> Ordem de A: ciclo de tamanho 3.
        Result resultado = processo.DefinirCronogramaFases(
            [FaseNaOrdem(faseA, 2), FaseNaOrdem(faseB, 3), FaseNaOrdem(faseC, 1)], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.PermutacaoDeOrdemNaoSuportada");
    }

    [Fact(DisplayName = "Contraprova: mover uma única fase retida para uma Ordem livre (nunca usada) é aceito — não é um ciclo")]
    public void DefinirCronogramaFases_MoveUmaFaseParaOrdemLivre_Aceita()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(5, "INSCRICAO", fase.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle(f => f.Id == fase.Id, "a Ordem 5 nunca foi usada — não há ciclo, e a reconciliação por identidade preserva o Id");
        processo.CronogramaFases.Single().Ordem.Should().Be(5);
    }

    [Fact(DisplayName = "Contraprova: inserir uma fase nova deslocando as demais (cadeia, não ciclo) é aceito")]
    public void DefinirCronogramaFases_InsereFaseDeslocandoAsDemais_Aceita()
    {
        FaseCronograma faseA = Fase(1, "A");
        FaseCronograma faseB = Fase(2, "B");
        ProcessoSeletivo processo = ProcessoComFases(faseA, faseB);

        // C é nova (Ordem 1); A desloca para 2; B desloca para 3 — uma CADEIA que termina
        // numa Ordem livre (3), não um ciclo.
        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "C"), FaseNaOrdem(faseA, 2), FaseNaOrdem(faseB, 3)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().Contain(f => f.Id == faseA.Id && f.Ordem == 2);
        processo.CronogramaFases.Should().Contain(f => f.Id == faseB.Id && f.Ordem == 3);
        processo.CronogramaFases.Should().ContainSingle(f => f.Codigo == "C" && f.Ordem == 1);
    }
}
