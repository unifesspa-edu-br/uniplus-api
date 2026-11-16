namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoCronograma;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.DefinirCronogramaFases"/> (Story #851
/// §3.3/§3.5): estrutura do cronograma (CA-06), precedência entre fases — inclusive a
/// contraprova de ausência (CA-08), sobreposição de janelas (CA-09), a aresta da
/// heteroidentificação (CA-10), a direção eager da bicondicional fase×etapa (CA-14) e a
/// conclusão do ciclo recursal por matéria.
/// </summary>
public sealed class ProcessoSeletivoCronogramaTests
{
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
        FaseCronograma fase2 = Fase(1, "HOMOLOGACAO_INSCRICOES").Value!;

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

    /// <summary>
    /// CA-14 mudou de lugar, não de conteúdo. Com a etapa declarando a fase a que pertence,
    /// ela só pode ser gravada depois que a fase existe no cronograma — cobrar a etapa na
    /// gravação da fase fecharia um ciclo sem ordem possível. A bicondicional continua
    /// valendo, e é o gate de publicação que a cobra.
    /// </summary>
    [Fact(DisplayName = "CA-14: fase que agrupa etapas grava sem etapa; quem recusa é a publicação")]
    public void Avaliacao_SemEtapa_GravaEFalhaNaPublicacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "AVALIACAO", agrupaEtapas: true).Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum, CorretoresDeTeste.SemConsolidacao)
            .Should().Contain(item =>
                item.Codigo == "cronograma_fase_agrupadora_sem_etapa_pontuada" && !item.Ok);
    }

    [Fact(DisplayName = "Fase que agrupa etapas é aceita quando o processo já tem etapa pontuada")]
    public void Avaliacao_ComEtapa_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!],
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
        FaseCronograma homologacao = Fase(1, "HOMOLOGACAO_INSCRICOES").Value!;
        FaseCronograma inscricao = Fase(2, "INSCRICAO", coletaInscricao: true).Value!;
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO_INSCRICOES")];

        Result resultado = processo.DefinirCronogramaFases([homologacao, inscricao], precedencias, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.PrecedenciaFaseViolada");
    }

    private static List<ArestaPrecedencia> ArestasEstruturaisDoCiclo() =>
    [
        Aresta("INSCRICAO", "HOMOLOGACAO_INSCRICOES"),
        Aresta("RESULTADO_PRELIMINAR", "RECURSOS"),
        Aresta("RECURSOS", "RESULTADO_FINAL"),
        Aresta("RESULTADO_FINAL", "HABILITACAO"),
        Aresta("HABILITACAO", "MATRICULA"),
        Aresta("HETEROIDENTIFICACAO", "HOMOLOGACAO_RESULTADO_FINAL"),
    ];

    [Fact(DisplayName = "CA-08 (contraprova): a AUSÊNCIA de uma das duas fases da aresta NÃO é violação — cronograma mínimo passa")]
    public void Precedencia_FaseAusente_NaoEhViolacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        // Cronograma mínimo: importação → classificação → habilitação, SEM homologação e SEM avaliação.
        FaseCronograma classificacao = Fase(1, "CLASSIFICACAO", produzResultado: true).Value!;
        FaseCronograma habilitacao = Fase(2, "HABILITACAO").Value!;

        Result resultado = processo.DefinirCronogramaFases([classificacao, habilitacao], ArestasEstruturaisDoCiclo(), PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── CA-09 — sobreposição de janelas ──

    [Fact(DisplayName = "CA-09: janelas de fases dependentes que se sobrepõem são recusadas quando o cadastro NÃO permite sobreposição")]
    public void Sobreposicao_NaoPermitida_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (FaseCronograma inscricao, FaseCronograma homologacao) = InscricaoEHomologacaoComJanelasSobrepostas();
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO_INSCRICOES", permiteSobreposicao: false)];

        Result resultado = processo.DefinirCronogramaFases([inscricao, homologacao], precedencias, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida");
    }

    [Fact(DisplayName = "CA-09 (contraprova): com PermiteSobreposicao=true no cadastro, a mesma configuração é aceita")]
    public void Sobreposicao_Permitida_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (FaseCronograma inscricao, FaseCronograma homologacao) = InscricaoEHomologacaoComJanelasSobrepostas();
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO_INSCRICOES", permiteSobreposicao: true)];

        Result resultado = processo.DefinirCronogramaFases([inscricao, homologacao], precedencias, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Redefinir o cronograma com a própria fase já gravada preserva as bancas requeridas dela")]
    public void Redefinir_ComAPropriaFaseJaGravada_PreservaBancasRequeridas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        BancaRequerida banca = BancaRequerida.Criar(Guid.CreateVersion7(), "BANCA_ANALISE_DOCUMENTAL", "Banca de teste", []);
        FaseCronograma heteroidentificacao = Fase(1, "HETEROIDENTIFICACAO", bancasRequeridas: [banca]).Value!;
        processo.DefinirCronogramaFases([heteroidentificacao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases([.. processo.CronogramaFases], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Single().BancasRequeridas.Select(b => b.Codigo)
            .Should().Equal("BANCA_ANALISE_DOCUMENTAL");
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
            "HOMOLOGACAO_INSCRICOES",
            produtos:
            [
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo),
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

    [Fact(DisplayName = "CA-10: fase sem concluinte que publica o preliminar de um tipo e só o definitivo de outro é recusada")]
    public void Conclusao_SemDeclaracaoComDefinitivoDeOutroTipo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(
            1,
            "AVALIACAO",
            produtos:
            [
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Definitivo),
            ]).Value!;

        Result resultado = processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("fases[0].faseConcluinteCodigo");
        erro.Error.Code.Should().Be("ProcessoSeletivo.ConclusaoNaoDeclarada");
        erro.Error.Message.Should().Contain("'RESULTADO_CLASSIFICACAO'");
    }
}
