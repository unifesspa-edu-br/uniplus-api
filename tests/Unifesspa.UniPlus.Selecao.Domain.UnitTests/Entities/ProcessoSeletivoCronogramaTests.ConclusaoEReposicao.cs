namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class ProcessoSeletivoCronogramaTests
{
    [Fact(DisplayName = "CA-09: fase que declara concluinte SEM publicar preliminar é recusada")]
    public void Conclusao_DeclaradaSemPreliminar_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(
            1, "RESULTADO_FINAL", produtos: Definitiva(), faseConcluinteCodigo: "OUTRA_FASE").Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar");
    }

    [Fact(DisplayName = "CA-09: fase que declara a SI MESMA como concluinte é recusada")]
    public void Conclusao_DeclaradaEmFaseQueSeConclui_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(
            1,
            "HOMOLOGACAO",
            produtos:
            [
                ProdutoDaFase.Criar("HOMOLOGACAO_PRELIMINAR", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("HOMOLOGACAO_DEFINITIVA", PapelProdutoFase.Definitivo),
            ],
            faseConcluinteCodigo: "HOMOLOGACAO").Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.ConclusaoDeclaradaEmFaseQueSeConclui");
    }

    [Fact(DisplayName = "A declaração vence a inferência: fase que publica alguma definitiva e ainda declara concluinte tem a DECLARAÇÃO conferida")]
    public void Conclusao_DeclaracaoVenceAInferencia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        // O caso que a auto-conclusão por fase não distingue: a definitiva publicada é do
        // GABARITO, e quem conclui o ciclo do resultado preliminar é a fase seguinte.
        FaseCronograma avaliacao = Fase(
            1,
            "AVALIACAO",
            produtos:
            [
                ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Definitivo),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ],
            faseConcluinteCodigo: "RESULTADO_FINAL").Value!;
        FaseCronograma resultadoFinal = Fase(2, "RESULTADO_FINAL", produtos: Definitiva()).Value!;

        Result resultado = processo.DefinirCronogramaFases(
            [avaliacao, resultadoFinal], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A declaração vence a inferência: concluinte declarada que não publica definitiva é recusada mesmo quando a própria fase publica uma")]
    public void Conclusao_DeclaracaoVenceAInferencia_ConcluinteInvalidaAindaRecusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma avaliacao = Fase(
            1,
            "AVALIACAO",
            produtos:
            [
                ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Definitivo),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ],
            faseConcluinteCodigo: "RECURSOS").Value!;
        FaseCronograma recursos = Fase(2, "RECURSOS").Value!;

        Result resultado = processo.DefinirCronogramaFases(
            [avaliacao, recursos], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue(
            "dar precedência à declaração é conferi-la, não aceitá-la de olhos fechados");
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo");
    }

    [Fact(DisplayName = "CA-10: concluinte que não está no cronograma é recusada")]
    public void Conclusao_ConcluinteForaDoCronograma_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(
            1, "RESULTADO_PRELIMINAR", produtos: Preliminar(), faseConcluinteCodigo: "FASE_INEXISTENTE").Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteForaDoCronograma");
    }

    [Fact(DisplayName = "CA-10: concluinte que está no cronograma mas NÃO publica definitiva é recusada")]
    public void Conclusao_ConcluinteSemDefinitiva_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma preliminar = Fase(
            1, "RESULTADO_PRELIMINAR", produtos: Preliminar(), faseConcluinteCodigo: "RECURSOS").Value!;
        FaseCronograma recursos = Fase(2, "RECURSOS").Value!;

        Result resultado = processo.DefinirCronogramaFases([preliminar, recursos], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo");
    }

    [Fact(DisplayName = "CA-10: concluinte que só publica o definitivo de OUTRO tipo de ato é recusada")]
    public void Conclusao_ConcluinteComDefinitivoDeOutroTipo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma abre = Fase(
            1, "RESULTADO_PRELIMINAR", produtos: Preliminar("GABARITO"), faseConcluinteCodigo: "RESULTADO_FINAL").Value!;
        FaseCronograma concluinte = Fase(2, "RESULTADO_FINAL", produtos: Definitiva("RESULTADO_CLASSIFICACAO")).Value!;

        Result resultado = processo.DefinirCronogramaFases([abre, concluinte], [], PrecondicaoIfMatch.Ausente);

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
        FaseCronograma abre = Fase(
            1,
            "AVALIACAO",
            produtos:
            [
                ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ],
            faseConcluinteCodigo: "RESULTADO_FINAL").Value!;
        FaseCronograma concluinte = Fase(2, "RESULTADO_FINAL", produtos: Definitiva("GABARITO")).Value!;

        Result resultado = processo.DefinirCronogramaFases([abre, concluinte], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain("'RESULTADO_CLASSIFICACAO'");
    }

    [Fact(DisplayName = "CA-10: a matéria que a própria fase fecha não precisa do definitivo na concluinte")]
    public void Conclusao_MateriaFechadaNaPropriaFase_NaoExigeDefinitivoNaConcluinte()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma abre = Fase(
            1,
            "AVALIACAO",
            produtos:
            [
                ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Definitivo),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ],
            faseConcluinteCodigo: "RESULTADO_FINAL").Value!;
        FaseCronograma concluinte = Fase(2, "RESULTADO_FINAL", produtos: Definitiva("RESULTADO_CLASSIFICACAO")).Value!;

        Result resultado = processo.DefinirCronogramaFases([abre, concluinte], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-10: concluinte de ORDEM anterior à fase que ela encerra é recusada")]
    public void Conclusao_ConcluinteAntecedeNaOrdem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma definitiva = Fase(1, "RESULTADO_FINAL", produtos: Definitiva()).Value!;
        FaseCronograma preliminar = Fase(
            2, "RESULTADO_PRELIMINAR", produtos: Preliminar(), faseConcluinteCodigo: "RESULTADO_FINAL").Value!;

        Result resultado = processo.DefinirCronogramaFases([definitiva, preliminar], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteAntecedeAConcluida");
    }

    [Fact(DisplayName = "CA-10: concluinte posterior na ordem mas cuja JANELA começa antes é recusada")]
    public void Conclusao_ConcluinteComecaAntes_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma preliminar = Fase(
            1, "RESULTADO_PRELIMINAR", produtos: Preliminar(), faseConcluinteCodigo: "RESULTADO_FINAL",
            inicio: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)).Value!;
        FaseCronograma definitiva = Fase(
            2, "RESULTADO_FINAL", produtos: Definitiva(),
            inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)).Value!;

        Result resultado = processo.DefinirCronogramaFases([preliminar, definitiva], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProcessoSeletivo.FaseConcluinteComecaAntesDaConcluida");
    }

    [Fact(DisplayName = "CA-10 (fronteira): sem janela declarada nas duas fases, só a ordem decide — a gravação é aceita")]
    public void Conclusao_SemJanelaDeclarada_SoAOrdemDecide_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma preliminar = Fase(
            1, "RESULTADO_PRELIMINAR", produtos: Preliminar(), faseConcluinteCodigo: "RESULTADO_FINAL").Value!;
        FaseCronograma definitiva = Fase(2, "RESULTADO_FINAL", produtos: Definitiva()).Value!;

        Result resultado = processo.DefinirCronogramaFases([preliminar, definitiva], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "ADR-0125: duas fases mal declaradas acumulam as duas violações, cada uma com o próprio índice")]
    public void Conclusao_DuasFasesMalDeclaradas_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma semDeclaracao = Fase(1, "RESULTADO_PRELIMINAR", produtos: Preliminar()).Value!;
        FaseCronograma declaracaoIndevida = Fase(
            2, "RESULTADO_FINAL", produtos: Definitiva(), faseConcluinteCodigo: "OUTRA_FASE").Value!;

        Result resultado = processo.DefinirCronogramaFases(
            [semDeclaracao, declaracaoIndevida], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["fases[0].faseConcluinteCodigo", "fases[1].faseConcluinteCodigo"]);
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ProcessoSeletivo.ConclusaoNaoDeclarada",
            "ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar",
        ]);
    }

    [Fact(DisplayName = "Cronograma vencedor substitui integralmente a coleção — Definir novamente troca tudo")]
    public void DefinirCronograma_SubstituiIntegralmente()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma primeira = Fase(1, "INSCRICAO", coletaInscricao: true).Value!;
        processo.DefinirCronogramaFases([primeira], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma segunda = Fase(1, "HOMOLOGACAO").Value!;
        Result resultado = processo.DefinirCronogramaFases([segunda], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be("HOMOLOGACAO");
    }
}
