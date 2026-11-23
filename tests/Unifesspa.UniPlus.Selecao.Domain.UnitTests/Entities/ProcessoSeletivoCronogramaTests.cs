namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
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

        Result resultado = processo.DefinirCronogramaFases([], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CronogramaFasesVazio");
    }

    [Fact(DisplayName = "CA-06: ordem duplicada é recusada")]
    public void DefinirCronograma_OrdemDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase1 = new ModeloDeFase("INSCRICAO", 1).Criar();
        FaseCronograma fase2 = new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 1).Criar();

        Result resultado = processo.DefinirCronogramaFases([fase1, fase2], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.OrdemFaseDuplicada");
    }

    [Fact(DisplayName = "CA-06: a mesma fase canônica duas vezes é recusada")]
    public void DefinirCronograma_FaseCanonicaDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid origemComum = Guid.CreateVersion7();
        FaseCronograma fase1 = new ModeloDeFase("INSCRICAO", 1) { OrigemId = origemComum }.Criar();
        FaseCronograma fase2 = new ModeloDeFase("INSCRICAO", 2) { OrigemId = origemComum }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase1, fase2], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseCanonicaDuplicada");
    }

    // ── CA-14 — bicondicional fase×etapa (direção eager) ──

    /// <summary>
    /// CA-14 mudou de lugar, não de conteúdo. Com a etapa declarando a fase a que pertence,
    /// ela só pode ser gravada depois que a fase existe no cronograma — cobrar a etapa na
    /// gravação da fase fecharia um ciclo sem ordem possível. A bicondicional continua
    /// valendo, e é a conferência de publicação que a cobra.
    /// </summary>
    [Fact(DisplayName = "CA-14: fase que agrupa etapas grava sem etapa; quem recusa é a publicação")]
    public void Avaliacao_SemEtapa_GravaEFalhaNaPublicacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = new ModeloDeFase("AVALIACAO", 1) { AgrupaEtapas = true }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase], []);

        resultado.IsSuccess.Should().BeTrue();
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Should().Contain(item =>
                item.Codigo == "cronograma_fase_agrupadora_sem_etapa_pontuada" && !item.Ok);
    }

    [Fact(DisplayName = "Fase que agrupa etapas é aceita quando o processo já tem etapa pontuada")]
    public void Avaliacao_ComEtapa_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!]);
        FaseCronograma fase = new ModeloDeFase("AVALIACAO", 1) { AgrupaEtapas = true }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── CA-08 — precedência, inclusive a contraprova de ausência ──

    [Fact(DisplayName = "CA-08: ordem que viola a precedência declarada no cadastro é recusada")]
    public void Precedencia_OrdemInvertida_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma homologacao = new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 1).Criar();
        FaseCronograma inscricao = new ModeloDeFase("INSCRICAO", 2) { ColetaInscricao = true }.Criar();
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO_INSCRICOES")];

        Result resultado = processo.DefinirCronogramaFases([homologacao, inscricao], precedencias);

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
        FaseCronograma classificacao = new ModeloDeFase("CLASSIFICACAO", 1) { Produtos = [ProdutoDaFase.Criar("CLASSIFICACAO", PapelProdutoFase.Definitivo)] }.Criar();
        FaseCronograma habilitacao = new ModeloDeFase("HABILITACAO", 2).Criar();

        Result resultado = processo.DefinirCronogramaFases([classificacao, habilitacao], ArestasEstruturaisDoCiclo());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── CA-09 — sobreposição de janelas ──

    [Fact(DisplayName = "CA-09: janelas de fases dependentes que se sobrepõem são recusadas quando o cadastro NÃO permite sobreposição")]
    public void Sobreposicao_NaoPermitida_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (FaseCronograma inscricao, FaseCronograma homologacao) = InscricaoEHomologacaoComPeriodosSobrepostos();
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO_INSCRICOES", permiteSobreposicao: false)];

        Result resultado = processo.DefinirCronogramaFases([inscricao, homologacao], precedencias);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida");
    }

    [Fact(DisplayName = "CA-09 (contraprova): com PermiteSobreposicao=true no cadastro, a mesma configuração é aceita")]
    public void Sobreposicao_Permitida_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        (FaseCronograma inscricao, FaseCronograma homologacao) = InscricaoEHomologacaoComPeriodosSobrepostos();
        List<ArestaPrecedencia> precedencias = [Aresta("INSCRICAO", "HOMOLOGACAO_INSCRICOES", permiteSobreposicao: true)];

        Result resultado = processo.DefinirCronogramaFases([inscricao, homologacao], precedencias);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Redefinir o cronograma com a própria fase já gravada preserva as bancas requeridas dela")]
    public void Redefinir_ComAPropriaFaseJaGravada_PreservaBancasRequeridas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        BancaRequerida banca = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Guid.CreateVersion7()), []);
        FaseCronograma heteroidentificacao = new ModeloDeFase("HETEROIDENTIFICACAO", 1) { Bancas = [banca] }.Criar();
        processo.DefinirCronogramaFases([heteroidentificacao], []).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases([.. processo.CronogramaFases], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Single().BancasRequeridas.Select(b => b.Tipo.Codigo)
            .Should().Equal(TipoDeBanca.AnaliseDocumental);
    }

    // ── CA-10 — heteroidentificação precede a homologação do resultado final ──

    [Fact(DisplayName = "CA-10: declarar a homologação do resultado final ANTES da heteroidentificação é recusado")]
    public void Heteroidentificacao_PrecedeHomologacaoDoResultadoFinal_OrdemInvertida_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma homologacaoResultadoFinal = new ModeloDeFase("HOMOLOGACAO_RESULTADO_FINAL", 7).Criar();
        FaseCronograma heteroidentificacao = new ModeloDeFase("HETEROIDENTIFICACAO", 8).Criar();
        List<ArestaPrecedencia> precedencias = [Aresta("HETEROIDENTIFICACAO", "HOMOLOGACAO_RESULTADO_FINAL")];

        Result resultado = processo.DefinirCronogramaFases(
            [homologacaoResultadoFinal, heteroidentificacao], precedencias);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.PrecedenciaFaseViolada");
    }

    // ── Fechamento do ciclo de recurso dentro da fase ──

    [Fact(DisplayName = "Fase que publica só o definitivo é aceita")]
    public void Fechamento_SoDefinitivo_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = new ModeloDeFase("RESULTADO_FINAL", 1) { Produtos = Definitiva() }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Fase que publica o preliminar e o definitivo do mesmo tipo de ato é aceita")]
    public void Fechamento_PreliminarEDefinitivoDoMesmoTipo_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 1)
        {
            Produtos = [
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo),
            ]
        }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A fase que publica só o preliminar é aceita na gravação: o fechamento do ciclo é conferido na publicação")]
    public void Fechamento_PreliminarSemDefinitivo_AceitaNaGravacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = new ModeloDeFase("RESULTADO_FINAL", 1) { Produtos = Preliminar() }.Criar();

        Result resultado = processo.DefinirCronogramaFases([fase], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
