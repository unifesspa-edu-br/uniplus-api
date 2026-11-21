namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoDesempatePorAreaEnem;

public sealed class ProcessoSeletivoDesempatePorAreaEnemClassificacaoTests
{
    [Fact(DisplayName = "Classificação que desmarca ENEM havendo desempate por área é recusada")]
    public void DefinirClassificacao_SemEnemComDesempatePorArea_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")]).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoEnem()).IsSuccess.Should().BeTrue();
        ConfiguracaoClassificacao anterior = processo.Classificacao!;

        Result result = processo.DefinirClassificacao(ClassificacaoSemEnem());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        processo.Classificacao.Should().BeSameAs(anterior, "a recusa acontece antes de qualquer escrita");
    }

    [Fact(DisplayName = "Na classificação, a recusa por desmarcar ENEM sai no campo baseadoEmEnem")]
    public void DefinirClassificacao_SemEnemComDesempatePorArea_RecusaNoCampoBaseadoEmEnem()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoSemEnem());

        result.Errors.Should().ContainSingle().Which.Field.Should().Be("baseadoEmEnem");
    }

    [Fact(DisplayName = "Na classificação importada, a recusa sem quadro sai no campo da regra de cálculo")]
    public void DefinirClassificacao_ImportadaDoEnemComDesempatePorArea_RecusaNoCampoDaRegraDeCalculo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaDoEnem());

        result.Errors.Should().ContainSingle().Which.Field.Should().Be("regraCalculoCodigo");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM e desempate sem quadro: cada campo da classificação sai uma vez só")]
    public void DefinirClassificacao_EtapaDoEnemEDesempateSemQuadro_CadaCampoUmaVez()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")]).IsSuccess.Should().BeTrue();
        AdicionarEtapasDeNotaDoEnem(processo, 1);

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaSemEnem());

        result.Errors.Select(static e => e.Field).Should().Equal("baseadoEmEnem", "regraCalculoCodigo");
        processo.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Com as duas causas, a recusa sem quadro sai nos dois campos da classificação")]
    public void DefinirClassificacao_ImportadaSemEnemComDesempatePorArea_RecusaNosDoisCampos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaSemEnem());

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
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO"), MaiorIdade(2), PorArea(3, "MATEMATICA")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaSemEnem());

        result.Errors.Select(static e => e.Field).Should().Equal("baseadoEmEnem", "regraCalculoCodigo");
    }

    [Fact(DisplayName = "Na classificação, a etapa inexistente da eliminação e o desempate sem quadro saem juntos")]
    public void DefinirClassificacao_EliminacaoOrfaEDesempateSemQuadro_Acumulam()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")]).IsSuccess.Should().BeTrue();
        RegraEliminacao eliminacaoDeEtapaInexistente = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, 'd'), new ParametrosElimNotaMinimaEtapa(Guid.CreateVersion7(), 50m)).Value!;

        Result result = processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ApoioDaConfiguracaoClassificacao.DeclaracaoMediaPonderada() with { RegrasEliminacao = [eliminacaoDeEtapaInexistente], EscalaNotaFinal = 100m }).Value!);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("regrasEliminacao[0].etapaRef", "ProcessoSeletivo.EtapaRefEliminacaoInexistente"),
            ("baseadoEmEnem", "ProcessoSeletivo.DesempateAreaEnemSemQuadro"));
    }

    [Fact(DisplayName = "Na classificação, a recusa por área fora do quadro sai no campo da resolução, uma por critério, com a lista uma vez")]
    public void DefinirClassificacao_QuadroSemAreasCitadas_UmaRecusaPorCriterio()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "MATEMATICA", "CIENCIAS_HUMANAS"), PorArea(2, "CIENCIAS_DA_NATUREZA")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoEnem(QuadroComGrupoSemMatematica()));

        result.Errors.Select(static e => e.Field).Should().Equal("resolucaoPesoAreaEnem", "resolucaoPesoAreaEnem");
        result.Errors[0].Error.Message.Should().Contain("ordem 1").And.Contain("MATEMATICA, CIENCIAS_HUMANAS").And.Contain("Áreas aceitas:");
        result.Errors[1].Error.Message.Should().Contain("ordem 2").And.Contain("CIENCIAS_DA_NATUREZA").And.NotContain("Áreas aceitas");
    }

    [Fact(DisplayName = "A mesma área citada por dois critérios por área é recusada no campo da segunda citação")]
    public void DefinirCriteriosDesempate_AreaCitadaPorDoisCriterios_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA"), MaiorIdade(2), PorArea(3, "LINGUAGENS", "REDACAO")]);

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        (erro.Field, erro.Error.Code).Should().Be(("criterios[2].parametros.areas[1]", "ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio"));
        erro.Error.Message.Should().Contain("REDACAO").And.Contain("ordem 1");
    }

    [Fact(DisplayName = "A área repetida entre critérios fora da ordem do desempate é recusada na citação de ordem maior")]
    public void DefinirCriteriosDesempate_AreaCitadaPorDoisCriteriosForaDeOrdem_RecusaAOrdemMaior()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirCriteriosDesempate([PorArea(2, "REDACAO"), PorArea(1, "REDACAO", "MATEMATICA")]);

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        (erro.Field, erro.Error.Code).Should().Be(("criterios[0].parametros.areas[0]", "ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio"));
        erro.Error.Message.Should().Contain("ordem 1");
    }

    [Fact(DisplayName = "Na classificação, a etapa de nota do ENEM já inválida, sozinha, sai como recusa sem campo")]
    public void DefinirClassificacao_EtapaNotaEnemDuplicada_SemCampo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DuplicarEtapaDeNotaDoEnem(processo);

        Result result = processo.DefinirClassificacao(ClassificacaoEnem());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
        result.Errors.Should().ContainSingle().Which.Field.Should().BeNull("o comando de classificação não tem campo de etapa");
    }

    [Fact(DisplayName = "Na classificação, a etapa de nota do ENEM já inválida não entra no lote das recusas de campo")]
    public void DefinirClassificacao_EtapaNotaEnemDuplicadaComRecusaDeCampo_SoRecusasDeCampo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA")]).IsSuccess.Should().BeTrue();
        DuplicarEtapaDeNotaDoEnem(processo);

        Result result = processo.DefinirClassificacao(ClassificacaoEnem(QuadroComGrupoSemMatematica()));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("resolucaoPesoAreaEnem", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        processo.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Classificação cujo quadro não tem uma área citada pelo desempate é recusada")]
    public void DefinirClassificacao_QuadroSemAreaCitada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoEnem(QuadroComGrupoSemMatematica()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
        result.Error.Message.Should().Contain("MATEMATICA");
        processo.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Classificação ENEM com as áreas citadas pelo desempate é aceita")]
    public void DefinirClassificacao_QuadroComAsAreasCitadas_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoEnem());

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Classificação sem ENEM continua valendo para processo sem desempate por área")]
    public void DefinirClassificacao_SemEnemSemDesempatePorArea_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([MaiorIdade(1)]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoSemEnem());

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }
}
