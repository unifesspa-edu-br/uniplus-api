namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.DefinirCronogramaFases"/> (Story #851
/// §3.3/§3.5): estrutura do cronograma (CA-06), precedência entre fases — inclusive a
/// contraprova de ausência (CA-08), sobreposição de janelas (CA-09), a aresta da
/// heteroidentificação (CA-10), a direção eager da bicondicional fase×etapa (CA-14) e a
/// conclusão do ciclo recursal por matéria.
/// </summary>
public sealed class ProcessoSeletivoCronogramaTests
{
    private static ProcessoSeletivo NovoProcesso(OrigemCandidatos origem = OrigemCandidatos.ImportacaoExterna) =>
        ProcessoSeletivo.Criar("PS Cronograma", TipoProcesso.SiSU, origem, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static Result<FaseCronograma> Fase(
        int ordem,
        string codigo,
        bool agrupaEtapas = false,
        bool produzResultado = false,
        bool coletaInscricao = false,
        DateTimeOffset? inicio = null,
        DateTimeOffset? fim = null,
        Guid? faseCanonicaOrigemId = null,
        bool coletaSolicitacaoIsencao = false,
        IReadOnlyList<ProdutoDaFase>? produtos = null,
        string? faseConcluinteCodigo = null,
        bool emiteParecerIndividual = false) =>
        FaseCronograma.Criar(
            ordem,
            faseCanonicaOrigemId ?? Guid.CreateVersion7(),
            codigo,
            "CEPS",
            OrigemDataFase.Delegada,
            agrupaEtapas,
            permiteComplementacao: false,
            coletaInscricao,
            coletaSolicitacaoIsencao,
            inicio,
            fim,
            produtos ?? (produzResultado ? [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)] : []),
            faseConcluinteCodigo,
            emiteParecerIndividual,
            bancasRequeridas: [],
            regraRecurso: null);

    private static ArestaPrecedencia Aresta(string antecessora, string sucessora, bool permiteSobreposicao = false) =>
        new(antecessora, sucessora, permiteSobreposicao);

    // ── CA-06 — estrutura ──

    [Fact(DisplayName = "CA-06: cronograma vazio é recusado")]
    public void DefinirCronograma_ListaVazia_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirCronogramaFases([], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CronogramaFasesVazio");
    }

    [Fact(DisplayName = "CA-06: ordem duplicada é recusada")]
    public void DefinirCronograma_OrdemDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase1 = Fase(1, "INSCRICAO").Value!;
        FaseCronograma fase2 = Fase(1, "HOMOLOGACAO").Value!;

        Result resultado = processo.DefinirCronogramaFases([fase1, fase2], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.OrdemFaseDuplicada");
    }

    [Fact(DisplayName = "CA-06: a mesma fase canônica duas vezes é recusada")]
    public void DefinirCronograma_FaseCanonicaDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid origemComum = Guid.CreateVersion7();
        FaseCronograma fase1 = Fase(1, "INSCRICAO", faseCanonicaOrigemId: origemComum).Value!;
        FaseCronograma fase2 = Fase(2, "INSCRICAO", faseCanonicaOrigemId: origemComum).Value!;

        Result resultado = processo.DefinirCronogramaFases([fase1, fase2], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseCanonicaDuplicada");
    }

    // ── CA-14 — bicondicional fase×etapa (direção eager) ──

    [Fact(DisplayName = "CA-14: fase que agrupa etapas é recusada quando o processo não tem NENHUMA etapa pontuada")]
    public void Avaliacao_SemEtapa_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "AVALIACAO", agrupaEtapas: true).Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AvaliacaoSemEtapa");
    }

    [Fact(DisplayName = "Fase que agrupa etapas é aceita quando o processo já tem etapa pontuada")]
    public void Avaliacao_ComEtapa_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente);
        FaseCronograma fase = Fase(1, "AVALIACAO", agrupaEtapas: true).Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── CA-08 — precedência, inclusive a contraprova de ausência ──

    [Fact(DisplayName = "CA-08: ordem que viola a precedência declarada no cadastro é recusada")]
    public void Precedencia_OrdemInvertida_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma homologacao = Fase(1, "HOMOLOGACAO").Value!;
        FaseCronograma inscricao = Fase(2, "INSCRICAO", coletaInscricao: true).Value!;
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO")];

        Result resultado = processo.DefinirCronogramaFases([homologacao, inscricao], precedencias, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.PrecedenciaFaseViolada");
    }

    [Fact(DisplayName = "CA-08 (contraprova): a AUSÊNCIA de uma das duas fases da aresta NÃO é violação — cronograma mínimo passa")]
    public void Precedencia_FaseAusente_NaoEhViolacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        // Cronograma mínimo: importação → classificação → habilitação, SEM homologação e SEM avaliação.
        FaseCronograma classificacao = Fase(1, "CLASSIFICACAO", produzResultado: true).Value!;
        FaseCronograma habilitacao = Fase(2, "HABILITACAO").Value!;
        List<ArestaPrecedencia> precedencias =
        [
            Aresta("INSCRICAO", "HOMOLOGACAO"),
            Aresta("RESULTADO_PRELIMINAR", "RECURSOS"),
            Aresta("RECURSOS", "RESULTADO_FINAL"),
            Aresta("RESULTADO_FINAL", "HABILITACAO"),
            Aresta("HABILITACAO", "MATRICULA"),
            Aresta("HETEROIDENTIFICACAO", "HOMOLOGACAO_RESULTADO_FINAL"),
        ];

        Result resultado = processo.DefinirCronogramaFases([classificacao, habilitacao], precedencias, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── CA-09 — sobreposição de janelas ──

    [Fact(DisplayName = "CA-09: janelas de fases dependentes que se sobrepõem são recusadas quando o cadastro NÃO permite sobreposição")]
    public void Sobreposicao_NaoPermitida_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma inscricao = Fase(
            1, "INSCRICAO", coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)).Value!;
        FaseCronograma homologacao = Fase(
            2, "HOMOLOGACAO",
            inicio: new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero)).Value!;
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO", permiteSobreposicao: false)];

        Result resultado = processo.DefinirCronogramaFases([inscricao, homologacao], precedencias, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida");
    }

    [Fact(DisplayName = "CA-09 (contraprova): com PermiteSobreposicao=true no cadastro, a mesma configuração é aceita")]
    public void Sobreposicao_Permitida_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma inscricao = Fase(
            1, "INSCRICAO", coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)).Value!;
        FaseCronograma homologacao = Fase(
            2, "HOMOLOGACAO",
            inicio: new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero)).Value!;
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO", permiteSobreposicao: true)];

        Result resultado = processo.DefinirCronogramaFases([inscricao, homologacao], precedencias, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── CA-10 — heteroidentificação precede a homologação do resultado final ──

    [Fact(DisplayName = "CA-10: declarar a homologação do resultado final ANTES da heteroidentificação é recusado")]
    public void Heteroidentificacao_PrecedeHomologacaoDoResultadoFinal_OrdemInvertida_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma homologacaoResultadoFinal = Fase(7, "HOMOLOGACAO_RESULTADO_FINAL").Value!;
        FaseCronograma heteroidentificacao = Fase(8, "HETEROIDENTIFICACAO").Value!;
        List<ArestaPrecedencia> precedencias = [Aresta("HETEROIDENTIFICACAO", "HOMOLOGACAO_RESULTADO_FINAL")];

        Result resultado = processo.DefinirCronogramaFases(
            [homologacaoResultadoFinal, heteroidentificacao], precedencias, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.PrecedenciaFaseViolada");
    }

    // ── Conclusão do ciclo recursal por matéria — os três estados legítimos e as violações ──

    private static IReadOnlyList<ProdutoDaFase> Preliminar(string atoCodigo = "RESULTADO_PRELIMINAR") =>
        [ProdutoDaFase.Criar(atoCodigo, PapelProdutoFase.Preliminar)];

    private static IReadOnlyList<ProdutoDaFase> Definitiva(string atoCodigo = "RESULTADO_FINAL") =>
        [ProdutoDaFase.Criar(atoCodigo, PapelProdutoFase.Definitivo)];

    [Fact(DisplayName = "Estado 1: fase que NÃO publica preliminar e não declara conclusão é aceita")]
    public void Conclusao_SemPreliminarESemDeclaracao_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "RESULTADO_FINAL", produtos: Definitiva()).Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Estado 2: fase que publica preliminar E definitiva conclui a si mesma e é aceita sem declarar concluinte")]
    public void Conclusao_ConcluiASiMesma_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(
            1,
            "HOMOLOGACAO",
            produtos:
            [
                ProdutoDaFase.Criar("HOMOLOGACAO_PRELIMINAR", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("HOMOLOGACAO_DEFINITIVA", PapelProdutoFase.Definitivo),
            ]).Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Estado 3: fase que publica só preliminar e aponta uma fase posterior que publica definitiva é aceita")]
    public void Conclusao_ApontaOutraFase_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma preliminar = Fase(
            1, "RESULTADO_PRELIMINAR", produtos: Preliminar(), faseConcluinteCodigo: "RESULTADO_FINAL",
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Value!;
        FaseCronograma definitiva = Fase(
            2, "RESULTADO_FINAL", produtos: Definitiva(),
            inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)).Value!;

        Result resultado = processo.DefinirCronogramaFases([preliminar, definitiva], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-09: fase que publica só preliminar SEM declarar concluinte é recusada")]
    public void Conclusao_PreliminarSemDeclaracao_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "RESULTADO_PRELIMINAR", produtos: Preliminar()).Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Field == "fases[0].faseConcluinteCodigo"
                && e.Error.Code == "ProcessoSeletivo.ConclusaoNaoDeclarada");
    }

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
                ProdutoDaFase.Criar("GABARITO_DEFINITIVO", PapelProdutoFase.Definitivo),
                ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar),
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
                ProdutoDaFase.Criar("GABARITO_DEFINITIVO", PapelProdutoFase.Definitivo),
                ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar),
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
