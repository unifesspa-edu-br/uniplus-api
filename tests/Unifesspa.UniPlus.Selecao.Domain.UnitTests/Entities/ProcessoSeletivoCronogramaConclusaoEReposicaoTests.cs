namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoCronograma;

public sealed class ProcessoSeletivoCronogramaConclusaoEReposicaoTests
{
    [Fact(DisplayName = "CA-09: fase que declara concluinte SEM publicar preliminar é recusada")]
    public void Conclusao_DeclaradaSemPreliminar_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = new ModeloDeFase("RESULTADO_FINAL", 1) { Produtos = Definitiva(), FaseConcluinteCodigo = "OUTRA_FASE" }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar");
    }

    private static ProdutoDaFase[] ProdutosPreliminarEDefinitivoDaHomologacao() =>
    [
        ProdutoDaFase.Criar("HOMOLOGACAO_PRELIMINAR", PapelProdutoFase.Preliminar),
        ProdutoDaFase.Criar("HOMOLOGACAO_DEFINITIVA", PapelProdutoFase.Definitivo),
    ];

    [Fact(DisplayName = "CA-09: fase que declara a SI MESMA como concluinte é recusada")]
    public void Conclusao_DeclaradaEmFaseQueSeConclui_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 1) { Produtos = ProdutosPreliminarEDefinitivoDaHomologacao(), FaseConcluinteCodigo = "HOMOLOGACAO_INSCRICOES" }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.ConclusaoDeclaradaEmFaseQueSeConclui");
    }

    private static FaseCronograma AvaliacaoComConcluinte(string faseConcluinteCodigo, params ProdutoDaFase[] produtos) =>
        new ModeloDeFase("AVALIACAO", 1) { Produtos = produtos, FaseConcluinteCodigo = faseConcluinteCodigo }.Criar();

    [Fact(DisplayName = "A declaração vence a inferência: fase que publica alguma definitiva e ainda declara concluinte tem a DECLARAÇÃO conferida")]
    public void Conclusao_DeclaracaoVenceAInferencia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        // O caso que a auto-conclusão por fase não distingue: a definitiva publicada é do
        // GABARITO, e quem conclui o ciclo do resultado preliminar é a fase seguinte.
        FaseCronograma avaliacao = AvaliacaoComConcluinte(
            "RESULTADO_FINAL",
            ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Definitivo),
            ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar));
        FaseCronograma resultadoFinal = new ModeloDeFase("RESULTADO_FINAL", 2) { Produtos = Definitiva() }.Criar();

        Result resultado = processo.DefinirCronogramaFases(
            [avaliacao, resultadoFinal], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A declaração vence a inferência: concluinte declarada que não publica definitiva é recusada mesmo quando a própria fase publica uma")]
    public void Conclusao_DeclaracaoVenceAInferencia_ConcluinteInvalidaAindaRecusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma avaliacao = AvaliacaoComConcluinte(
            "RECURSOS",
            ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Definitivo),
            ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar));
        FaseCronograma recursos = new ModeloDeFase("RECURSOS", 2).Criar();

        Result resultado = processo.DefinirCronogramaFases(
            [avaliacao, recursos], []);

        resultado.IsFailure.Should().BeTrue(
            "dar precedência à declaração é conferi-la, não aceitá-la de olhos fechados");
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo");
    }

    [Fact(DisplayName = "CA-10: concluinte que não está no cronograma é recusada")]
    public void Conclusao_ConcluinteForaDoCronograma_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = new ModeloDeFase("RESULTADO_PRELIMINAR", 1) { Produtos = Preliminar(), FaseConcluinteCodigo = "FASE_INEXISTENTE" }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteForaDoCronograma");
    }

    [Fact(DisplayName = "CA-10: concluinte que está no cronograma mas NÃO publica definitiva é recusada")]
    public void Conclusao_ConcluinteSemDefinitiva_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma preliminar = new ModeloDeFase("RESULTADO_PRELIMINAR", 1) { Produtos = Preliminar(), FaseConcluinteCodigo = "RECURSOS" }.Criar();
        FaseCronograma recursos = new ModeloDeFase("RECURSOS", 2).Criar();

        Result resultado = processo.DefinirCronogramaFases([preliminar, recursos], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo");
    }

    [Fact(DisplayName = "CA-10: concluinte que só publica o definitivo de OUTRO tipo de ato é recusada")]
    public void Conclusao_ConcluinteComDefinitivoDeOutroTipo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma abre = new ModeloDeFase("RESULTADO_PRELIMINAR", 1) { Produtos = Preliminar("GABARITO"), FaseConcluinteCodigo = "RESULTADO_FINAL" }.Criar();
        FaseCronograma concluinte = new ModeloDeFase("RESULTADO_FINAL", 2) { Produtos = Definitiva("RESULTADO_CLASSIFICACAO") }.Criar();

        Result resultado = processo.DefinirCronogramaFases([abre, concluinte], []);

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("fases[0].faseConcluinteCodigo");
        erro.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo");
        erro.Error.Message.Should().Contain("'GABARITO'");
    }

    [Fact(DisplayName = "CA-10: concluinte precisa fechar cada matéria que a fase deixa aberta, e não só uma")]
    public void Conclusao_ConcluinteQueFechaSoUmaDasMaterias_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma abre = AvaliacaoComConcluinte(
            "RESULTADO_FINAL",
            ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar),
            ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar));
        FaseCronograma concluinte = new ModeloDeFase("RESULTADO_FINAL", 2) { Produtos = Definitiva("GABARITO") }.Criar();

        Result resultado = processo.DefinirCronogramaFases([abre, concluinte], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain("'RESULTADO_CLASSIFICACAO'");
    }

    [Fact(DisplayName = "CA-10: a matéria que a própria fase fecha não precisa do definitivo na concluinte")]
    public void Conclusao_MateriaFechadaNaPropriaFase_NaoExigeDefinitivoNaConcluinte()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma abre = AvaliacaoComConcluinte(
            "RESULTADO_FINAL",
            ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar),
            ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Definitivo),
            ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar));
        FaseCronograma concluinte = new ModeloDeFase("RESULTADO_FINAL", 2) { Produtos = Definitiva("RESULTADO_CLASSIFICACAO") }.Criar();

        Result resultado = processo.DefinirCronogramaFases([abre, concluinte], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-10: concluinte de ORDEM anterior à fase que ela encerra é recusada")]
    public void Conclusao_ConcluinteAntecedeNaOrdem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma definitiva = new ModeloDeFase("RESULTADO_FINAL", 1) { Produtos = Definitiva() }.Criar();
        FaseCronograma preliminar = new ModeloDeFase("RESULTADO_PRELIMINAR", 2) { Produtos = Preliminar(), FaseConcluinteCodigo = "RESULTADO_FINAL" }.Criar();

        Result resultado = processo.DefinirCronogramaFases([definitiva, preliminar], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteAntecedeAConcluida");
    }

    [Fact(DisplayName = "CA-10: concluinte posterior na ordem mas cuja JANELA começa antes é recusada")]
    public void Conclusao_ConcluinteComecaAntes_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma preliminar = new ModeloDeFase("RESULTADO_PRELIMINAR", 1) { Produtos = Preliminar(), FaseConcluinteCodigo = "RESULTADO_FINAL", Inicio = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero) }.Criar();
        FaseCronograma definitiva = new ModeloDeFase("RESULTADO_FINAL", 2) { Produtos = Definitiva(), Inicio = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero) }.Criar();

        Result resultado = processo.DefinirCronogramaFases([preliminar, definitiva], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteComecaAntesDaConcluida");
    }

    [Fact(DisplayName = "CA-10 (fronteira): sem janela declarada nas duas fases, só a ordem decide — a gravação é aceita")]
    public void Conclusao_SemJanelaDeclarada_SoAOrdemDecide_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma preliminar = new ModeloDeFase("RESULTADO_PRELIMINAR", 1) { Produtos = Preliminar(), FaseConcluinteCodigo = "RESULTADO_FINAL" }.Criar();
        FaseCronograma definitiva = new ModeloDeFase("RESULTADO_FINAL", 2) { Produtos = Definitiva() }.Criar();

        Result resultado = processo.DefinirCronogramaFases([preliminar, definitiva], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "ADR-0125: duas fases mal declaradas acumulam as duas violações, cada uma com o próprio índice")]
    public void Conclusao_DuasFasesMalDeclaradas_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma semDeclaracao = new ModeloDeFase("RESULTADO_PRELIMINAR", 1) { Produtos = Preliminar() }.Criar();
        FaseCronograma declaracaoIndevida = new ModeloDeFase("RESULTADO_FINAL", 2) { Produtos = Definitiva(), FaseConcluinteCodigo = "OUTRA_FASE" }.Criar();

        Result resultado = processo.DefinirCronogramaFases([semDeclaracao, declaracaoIndevida], []);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoNaoDeclarada"),
            ("fases[1].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar"),
        ]);
    }

    [Fact(DisplayName = "Cronograma vencedor substitui integralmente a coleção — Definir novamente troca tudo")]
    public void DefinirCronograma_SubstituiIntegralmente()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma primeira = new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Criar();
        processo.DefinirCronogramaFases([primeira], []).IsSuccess.Should().BeTrue();

        FaseCronograma segunda = new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 1).Criar();
        Result resultado = processo.DefinirCronogramaFases([segunda], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be("HOMOLOGACAO_INSCRICOES");
    }
}
