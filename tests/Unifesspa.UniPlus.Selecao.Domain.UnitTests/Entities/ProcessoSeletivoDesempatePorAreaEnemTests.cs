namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O desempate por área do ENEM cita áreas pelo código, e a única fonte de área no processo é
/// o quadro de pesos por área congelado na classificação. A coerência é conferida dos dois
/// lados, porque o desempate é gravado antes da classificação no fluxo de configuração.
/// </summary>
public sealed class ProcessoSeletivoDesempatePorAreaEnemTests
{
    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS ENEM 2027", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static CriterioDesempate PorArea(int ordem, params string[] areas) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(areas)).Value!;

    private static CriterioDesempate MaiorIdade(int ordem) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!;

    private static ConfiguracaoClassificacao ClassificacaoEnem(IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro = null) =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            1,
            [],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            quadro ?? QuadroPesoAreaEnemDeTeste.Completo()).Value!;

    private static ConfiguracaoClassificacao ClassificacaoSemEnem() =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            1,
            [],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;

    private static ConfiguracaoClassificacao ClassificacaoImportadaDoEnem() =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            null,
            null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            1,
            [],
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;

    private static ConfiguracaoClassificacao ClassificacaoImportadaSemEnem() =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            null,
            null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            1,
            [],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;

    /// <summary>Quadro em que um dos grupos não tem a área de Matemática.</summary>
    private static IReadOnlyList<GrupoPesoAreaEnemCongelado> QuadroComGrupoSemMatematica() =>
    [
        QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I"),
        GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA",
            "Tecnológica",
            "Resolução nº 805/2024/Consepe – Anexo I",
            [
                ("REDACAO", "Redação", 2.00m, 400m),
                ("LINGUAGENS", "Linguagens e suas Tecnologias", 1.00m, null),
            ]).Value!,
    ];

    [Fact(DisplayName = "Processo novo, sem classificação, aceita o desempate por área — a coerência fica para a classificação")]
    public void DefinirCriteriosDesempate_SemClassificacao_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA")], PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.CriteriosDesempate.Should().ContainSingle()
            .Which.Args.Should().BeOfType<ArgsDesempateMaiorNotaAreaEnem>()
            .Which.Areas.Should().Equal("REDACAO", "MATEMATICA");
    }

    [Fact(DisplayName = "Desempate por área com áreas do quadro congelado é aceito")]
    public void DefinirCriteriosDesempate_AreasDoQuadro_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate(
            [PorArea(1, "REDACAO", "MATEMATICA", "LINGUAGENS"), MaiorIdade(2)], PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Desempate por área sob classificação que não é ENEM é recusado")]
    public void DefinirCriteriosDesempate_ClassificacaoSemEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoSemEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([MaiorIdade(1), PorArea(2, "REDACAO")], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        result.Error.Message.Should().Contain("ordem 2");
        processo.CriteriosDesempate.Should().BeEmpty("a recusa acontece antes de qualquer escrita");
    }

    [Fact(DisplayName = "Desempate por área sob classificação ENEM importada, sem quadro de pesos, é recusado")]
    public void DefinirCriteriosDesempate_ClassificacaoImportada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoImportadaDoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
    }

    [Fact(DisplayName = "Área fora do quadro e citada por outro critério recebe só a recusa do quadro, uma por campo")]
    public void DefinirCriteriosDesempate_AreaForaDoQuadroCitadaPorDoisCriterios_UmaRecusaPorCampo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "FISICA"), PorArea(2, "FISICA")], PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
            ("criterios[1].areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        result.Errors[0].Error.Message.Should().Contain("Áreas aceitas:");
    }

    [Fact(DisplayName = "Área fora do quadro congelado é recusada, listando os códigos aceitos com o rótulo ao lado")]
    public void DefinirCriteriosDesempate_AreaForaDoQuadro_RecusaListandoAsAceitas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "FISICA")], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
        result.Error.Message.Should().Contain("FISICA")
            .And.Contain("CIENCIAS_DA_NATUREZA (Ciências da Natureza e suas Tecnologias)")
            .And.Contain("CIENCIAS_HUMANAS (Ciências Humanas e suas Tecnologias)")
            .And.Contain("LINGUAGENS (Linguagens e suas Tecnologias)")
            .And.Contain("MATEMATICA (Matemática e suas Tecnologias)")
            .And.Contain("REDACAO (Redação)");
    }

    [Fact(DisplayName = "Áreas fora do quadro em dois critérios acumulam, cada recusa no campo da área")]
    public void DefinirCriteriosDesempate_AreasForaDoQuadroEmDoisCriterios_AcumulaNosCampos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate(
            [PorArea(1, "REDACAO", "FISICA"), MaiorIdade(2), PorArea(3, "QUIMICA")], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].areas[1]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
            ("criterios[2].areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        result.Errors[0].Error.Message.Should().Contain("FISICA").And.Contain("ordem 1");
        result.Errors[1].Error.Message.Should().Contain("QUIMICA").And.Contain("ordem 3");
    }

    [Fact(DisplayName = "Lista de critérios acima do teto é recusada inteira, com um único erro")]
    public void DefinirCriteriosDesempate_AcimaDoTeto_RecusaComUmErro()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        CriterioDesempate[] criterios = [.. Enumerable.Range(1, ProcessoSeletivo.CriteriosDesempateMaximo + 1).Select(static ordem => PorArea(ordem, "FISICA"))];

        Result result = processo.DefinirCriteriosDesempate(criterios, PrecondicaoIfMatch.Ausente);

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("criterios");
        erro.Error.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
        processo.CriteriosDesempate.Should().BeEmpty();
    }

    [Fact(DisplayName = "Muitas áreas fora do quadro geram resposta limitada: a lista de aceitas vai uma vez só")]
    public void DefinirCriteriosDesempate_MuitasAreasForaDoQuadro_RespostaLimitada()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        CriterioDesempate[] criterios = [.. Enumerable.Range(1, ProcessoSeletivo.CriteriosDesempateMaximo).Select(ordem =>
            PorArea(ordem, [.. Enumerable.Range(0, CriterioDesempate.AreasMaximo).Select(i => $"AREA_{ordem}_{i}")]))];

        Result result = processo.DefinirCriteriosDesempate(criterios, PrecondicaoIfMatch.Ausente);

        result.Errors.Should().HaveCount(ProcessoSeletivo.CriteriosDesempateMaximo * CriterioDesempate.AreasMaximo);
        result.Errors.Count(static e => e.Error.Message.Contains("Áreas aceitas", StringComparison.Ordinal)).Should().Be(1);
        result.Errors[0].Error.Message.Should().Contain("Áreas aceitas: CIENCIAS_DA_NATUREZA");
        result.Errors.Sum(static e => e.Error.Message.Length).Should().BeLessThan(400 * 160,
            "sem a lista repetida, cada recusa por área é uma frase curta");
    }

    [Fact(DisplayName = "Ordem duplicada, etapa inexistente e área fora do quadro acumulam, cada uma no campo do item")]
    public void DefinirCriteriosDesempate_ViolacoesDeNaturezasDiferentes_Acumulam()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        CriterioDesempate porEtapa = CriterioDesempate.Criar(
            2, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(Guid.CreateVersion7())).Value!;

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "FISICA"), porEtapa, MaiorIdade(1)], PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("criterios[0].areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
            ("criterios[1].etapaRef", "ProcessoSeletivo.EtapaRefDesempateInexistente"),
            ("criterios[2].ordem", "ProcessoSeletivo.OrdemDesempateDuplicada"),
        ]);
    }

    [Fact(DisplayName = "Desempate por área sem quadro acumula uma recusa por critério, no campo da regra")]
    public void DefinirCriteriosDesempate_SemQuadroEmDoisCriterios_AcumulaNoCampoDaRegra()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoSemEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO"), PorArea(2, "MATEMATICA")], PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].regraCodigo", "ProcessoSeletivo.DesempateAreaEnemSemQuadro"),
            ("criterios[1].regraCodigo", "ProcessoSeletivo.DesempateAreaEnemSemQuadro"));
    }

    [Fact(DisplayName = "Área que falta em algum grupo do quadro é recusada: o candidato desse grupo ficaria sem a nota")]
    public void DefinirCriteriosDesempate_AreaAusenteEmUmGrupo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(QuadroComGrupoSemMatematica()), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "MATEMATICA")], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
        result.Error.Message.Should().Contain("Áreas aceitas: LINGUAGENS (Linguagens e suas Tecnologias); REDACAO (Redação).");
    }

    [Fact(DisplayName = "Classificação que desmarca ENEM havendo desempate por área é recusada")]
    public void DefinirClassificacao_SemEnemComDesempatePorArea_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        ConfiguracaoClassificacao anterior = processo.Classificacao!;

        Result result = processo.DefinirClassificacao(ClassificacaoSemEnem(), PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        processo.Classificacao.Should().BeSameAs(anterior, "a recusa acontece antes de qualquer escrita");
    }

    [Fact(DisplayName = "Na classificação, a recusa por desmarcar ENEM sai no campo baseadoEmEnem")]
    public void DefinirClassificacao_SemEnemComDesempatePorArea_RecusaNoCampoBaseadoEmEnem()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoSemEnem(), PrecondicaoIfMatch.Ausente);

        result.Errors.Should().ContainSingle().Which.Field.Should().Be("baseadoEmEnem");
    }

    [Fact(DisplayName = "Na classificação importada, a recusa sem quadro sai no campo da regra de cálculo")]
    public void DefinirClassificacao_ImportadaDoEnemComDesempatePorArea_RecusaNoCampoDaRegraDeCalculo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaDoEnem(), PrecondicaoIfMatch.Ausente);

        result.Errors.Should().ContainSingle().Which.Field.Should().Be("regraCalculoCodigo");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM e desempate sem quadro: cada campo da classificação sai uma vez só")]
    public void DefinirClassificacao_EtapaDoEnemEDesempateSemQuadro_CadaCampoUmaVez()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        AdicionarEtapasDeNotaDoEnem(processo, 1);

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaSemEnem(), PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => e.Field).Should().Equal("baseadoEmEnem", "regraCalculoCodigo");
        processo.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Com as duas causas, a recusa sem quadro sai nos dois campos da classificação")]
    public void DefinirClassificacao_ImportadaSemEnemComDesempatePorArea_RecusaNosDoisCampos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaSemEnem(), PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("baseadoEmEnem", "ProcessoSeletivo.DesempateAreaEnemSemQuadro"),
            ("regraCalculoCodigo", "ProcessoSeletivo.DesempateAreaEnemSemQuadro"),
        ]);
    }

    [Fact(DisplayName = "Com vários critérios por área sem quadro, cada campo da classificação sai uma vez só")]
    public void DefinirClassificacao_VariosCriteriosSemQuadro_CadaCampoUmaVez()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO"), MaiorIdade(2), PorArea(3, "MATEMATICA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaSemEnem(), PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => e.Field).Should().Equal("baseadoEmEnem", "regraCalculoCodigo");
    }

    [Fact(DisplayName = "Na classificação, a etapa inexistente da eliminação e o desempate sem quadro saem juntos")]
    public void DefinirClassificacao_EliminacaoOrfaEDesempateSemQuadro_Acumulam()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            1,
            [RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, 'd'), new ArgsElimNotaMinimaEtapa(Guid.CreateVersion7(), 50m)).Value!],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;

        Result result = processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("regrasEliminacao[0].etapaRef", "ProcessoSeletivo.EtapaRefEliminacaoInexistente"),
            ("baseadoEmEnem", "ProcessoSeletivo.DesempateAreaEnemSemQuadro"));
    }

    [Fact(DisplayName = "Na classificação, a recusa por área fora do quadro sai no campo da resolução, uma por critério, com a lista uma vez")]
    public void DefinirClassificacao_QuadroSemAreasCitadas_UmaRecusaPorCriterio()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "MATEMATICA", "CIENCIAS_HUMANAS"), PorArea(2, "CIENCIAS_DA_NATUREZA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoEnem(QuadroComGrupoSemMatematica()), PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => e.Field).Should().Equal("resolucaoPesoAreaEnem", "resolucaoPesoAreaEnem");
        result.Errors[0].Error.Message.Should().Contain("ordem 1").And.Contain("MATEMATICA, CIENCIAS_HUMANAS").And.Contain("Áreas aceitas:");
        result.Errors[1].Error.Message.Should().Contain("ordem 2").And.Contain("CIENCIAS_DA_NATUREZA").And.NotContain("Áreas aceitas");
    }

    [Fact(DisplayName = "A mesma área citada por dois critérios por área é recusada no campo da segunda citação")]
    public void DefinirCriteriosDesempate_AreaCitadaPorDoisCriterios_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA"), MaiorIdade(2), PorArea(3, "LINGUAGENS", "REDACAO")], PrecondicaoIfMatch.Ausente);

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        (erro.Field, erro.Error.Code).Should().Be(("criterios[2].areas[1]", "ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio"));
        erro.Error.Message.Should().Contain("REDACAO").And.Contain("ordem 1");
    }

    [Fact(DisplayName = "A área repetida entre critérios fora da ordem do desempate é recusada na citação de ordem maior")]
    public void DefinirCriteriosDesempate_AreaCitadaPorDoisCriteriosForaDeOrdem_RecusaAOrdemMaior()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirCriteriosDesempate([PorArea(2, "REDACAO"), PorArea(1, "REDACAO", "MATEMATICA")], PrecondicaoIfMatch.Ausente);

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        (erro.Field, erro.Error.Code).Should().Be(("criterios[0].areas[0]", "ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio"));
        erro.Error.Message.Should().Contain("ordem 1");
    }

    [Fact(DisplayName = "Na classificação, a etapa de nota do ENEM já inválida, sozinha, sai como recusa sem campo")]
    public void DefinirClassificacao_EtapaNotaEnemDuplicada_SemCampo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DuplicarEtapaDeNotaDoEnem(processo);

        Result result = processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
        result.Errors.Should().ContainSingle().Which.Field.Should().BeNull("o comando de classificação não tem campo de etapa");
    }

    [Fact(DisplayName = "Na classificação, a etapa de nota do ENEM já inválida não entra no lote das recusas de campo")]
    public void DefinirClassificacao_EtapaNotaEnemDuplicadaComRecusaDeCampo_SoRecusasDeCampo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DuplicarEtapaDeNotaDoEnem(processo);

        Result result = processo.DefinirClassificacao(ClassificacaoEnem(QuadroComGrupoSemMatematica()), PrecondicaoIfMatch.Ausente);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("resolucaoPesoAreaEnem", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        processo.Classificacao.Should().BeNull();
    }

    private static void DuplicarEtapaDeNotaDoEnem(ProcessoSeletivo processo) => AdicionarEtapasDeNotaDoEnem(processo, 2);

    private static void AdicionarEtapasDeNotaDoEnem(ProcessoSeletivo processo, int quantidade)
    {
        EtapaProcesso Etapa(int ordem) => EtapaProcesso.Criar("Nota do ENEM", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, peso: 1m, ordem: ordem).Value!;
        System.Reflection.FieldInfo etapas = typeof(ProcessoSeletivo).GetField("_etapas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((List<EtapaProcesso>)etapas.GetValue(processo)!).AddRange(Enumerable.Range(1, quantidade).Select(Etapa));
    }

    [Fact(DisplayName = "Classificação cujo quadro não tem uma área citada pelo desempate é recusada")]
    public void DefinirClassificacao_QuadroSemAreaCitada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoEnem(QuadroComGrupoSemMatematica()), PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
        result.Error.Message.Should().Contain("MATEMATICA");
        processo.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Classificação ENEM com as áreas citadas pelo desempate é aceita")]
    public void DefinirClassificacao_QuadroComAsAreasCitadas_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoEnem(), PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Classificação sem ENEM continua valendo para processo sem desempate por área")]
    public void DefinirClassificacao_SemEnemSemDesempatePorArea_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([MaiorIdade(1)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoSemEnem(), PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }
}
