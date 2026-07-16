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
/// heteroidentificação (CA-10) e a direção eager da bicondicional fase×etapa (CA-14).
/// </summary>
public sealed class ProcessoSeletivoCronogramaTests
{
    private static ProcessoSeletivo NovoProcesso(OrigemCandidatos origem = OrigemCandidatos.ImportacaoExterna) =>
        ProcessoSeletivo.Criar("PS Cronograma", TipoProcesso.SiSU, origem);

    private static Result<FaseCronograma> Fase(
        int ordem,
        string codigo,
        bool agrupaEtapas = false,
        bool produzResultado = false,
        bool resultadoDefinitivo = false,
        bool coletaInscricao = false,
        DateTimeOffset? inicio = null,
        DateTimeOffset? fim = null,
        Guid? faseCanonicaOrigemId = null) =>
        FaseCronograma.Criar(
            ordem,
            faseCanonicaOrigemId ?? Guid.CreateVersion7(),
            codigo,
            "CEPS",
            OrigemDataFase.Delegada,
            agrupaEtapas,
            permiteComplementacao: false,
            produzResultado,
            resultadoDefinitivo,
            coletaInscricao,
            inicio,
            fim,
            atoProduzidoCodigo: produzResultado ? codigo : null,
            atoProduzidoEfeitoIrreversivel: false,
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
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
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
            1, "INSCRICAO", coletaInscricao: true,
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
            1, "INSCRICAO", coletaInscricao: true,
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
