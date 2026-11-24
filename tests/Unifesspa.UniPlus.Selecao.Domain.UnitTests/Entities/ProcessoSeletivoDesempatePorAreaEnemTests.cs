namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoDesempatePorAreaEnem;

/// <summary>
/// O desempate por área do ENEM cita áreas pelo código, e a única fonte de área no processo é
/// o quadro de pesos por área congelado na classificação. A coerência é conferida dos dois
/// lados, porque o desempate é gravado antes da classificação no fluxo de configuração.
/// </summary>
public sealed class ProcessoSeletivoDesempatePorAreaEnemTests
{
    [Fact(DisplayName = "Processo novo, sem classificação, aceita o desempate por área — a coerência fica para a classificação")]
    public void DefinirCriteriosDesempate_SemClassificacao_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "MATEMATICA")]);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.CriteriosDesempate.Should().ContainSingle()
            .Which.Parametros.Should().BeOfType<ParametrosDesempateMaiorNotaAreaEnem>()
            .Which.Areas.Should().Equal("REDACAO", "MATEMATICA");
    }

    [Fact(DisplayName = "Desempate por área com áreas do quadro congelado é aceito")]
    public void DefinirCriteriosDesempate_AreasDoQuadro_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem()).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate(
            [PorArea(1, "REDACAO", "MATEMATICA", "LINGUAGENS"), MaiorIdade(2)]);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Desempate por área sob classificação que não é ENEM é recusado")]
    public void DefinirCriteriosDesempate_ClassificacaoSemEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoSemEnem()).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([MaiorIdade(1), PorArea(2, "REDACAO")]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        result.Error.Message.Should().Contain("ordem 2");
        processo.CriteriosDesempate.Should().BeEmpty("a recusa acontece antes de qualquer escrita");
    }

    [Fact(DisplayName = "Desempate por área sob classificação ENEM importada, sem quadro de pesos, é recusado")]
    public void DefinirCriteriosDesempate_ClassificacaoImportada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoImportadaDoEnem()).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO")]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
    }

    [Fact(DisplayName = "Área fora do quadro e citada por outro critério recebe só a recusa do quadro, uma por campo")]
    public void DefinirCriteriosDesempate_AreaForaDoQuadroCitadaPorDoisCriterios_UmaRecusaPorCampo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem()).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "FISICA"), PorArea(2, "FISICA")]);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].parametros.areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
            ("criterios[1].parametros.areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        result.Errors[0].Error.Message.Should().Contain("Áreas aceitas:");
    }

    [Fact(DisplayName = "Área fora do quadro congelado é recusada, listando os códigos aceitos com o rótulo ao lado")]
    public void DefinirCriteriosDesempate_AreaForaDoQuadro_RecusaListandoAsAceitas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem()).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO", "FISICA")]);

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
        processo.DefinirClassificacao(ClassificacaoEnem()).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate(
            [PorArea(1, "REDACAO", "FISICA"), MaiorIdade(2), PorArea(3, "QUIMICA")]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].parametros.areas[1]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
            ("criterios[2].parametros.areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        result.Errors[0].Error.Message.Should().Contain("FISICA").And.Contain("ordem 1");
        result.Errors[1].Error.Message.Should().Contain("QUIMICA").And.Contain("ordem 3");
    }

    [Fact(DisplayName = "Lista de critérios acima do teto é recusada inteira, com um único erro")]
    public void DefinirCriteriosDesempate_AcimaDoTeto_RecusaComUmErro()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem()).IsSuccess.Should().BeTrue();
        CriterioDesempate[] criterios = [.. Enumerable.Range(1, RegrasDoDesempate.CriteriosDesempateMaximo + 1).Select(static ordem => PorArea(ordem, "FISICA"))];

        Result result = processo.DefinirCriteriosDesempate(criterios);

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("criterios");
        erro.Error.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
        processo.CriteriosDesempate.Should().BeEmpty();
    }

    [Fact(DisplayName = "Muitas áreas fora do quadro geram resposta limitada: a lista de aceitas vai uma vez só")]
    public void DefinirCriteriosDesempate_MuitasAreasForaDoQuadro_RespostaLimitada()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem()).IsSuccess.Should().BeTrue();
        CriterioDesempate[] criterios = [.. Enumerable.Range(1, RegrasDoDesempate.CriteriosDesempateMaximo).Select(ordem =>
            PorArea(ordem, [.. Enumerable.Range(0, ParametrosDesempateMaiorNotaAreaEnem.AreasMaximo).Select(i => $"AREA_{ordem}_{i}")]))];

        Result result = processo.DefinirCriteriosDesempate(criterios);

        result.Errors.Should().HaveCount(RegrasDoDesempate.CriteriosDesempateMaximo * ParametrosDesempateMaiorNotaAreaEnem.AreasMaximo);
        result.Errors.Count(static e => e.Error.Message.Contains("Áreas aceitas", StringComparison.Ordinal)).Should().Be(1);
        result.Errors[0].Error.Message.Should().Contain("Áreas aceitas: CIENCIAS_DA_NATUREZA");
        result.Errors.Sum(static e => e.Error.Message.Length).Should().BeLessThan(400 * 160,
            "sem a lista repetida, cada recusa por área é uma frase curta");
    }

    [Fact(DisplayName = "Ordem duplicada, etapa inexistente e área fora do quadro acumulam, cada uma no campo do item")]
    public void DefinirCriteriosDesempate_ViolacoesDeNaturezasDiferentes_Acumulam()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem()).IsSuccess.Should().BeTrue();
        CriterioDesempate porEtapa = CriterioDesempate.Criar(
            2, Regra(RegraDeDesempate.MaiorNotaNaEtapa.Codigo(), 'd'), new ParametrosDesempateMaiorNotaEtapa(Guid.CreateVersion7())).Value!;

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "FISICA"), porEtapa, MaiorIdade(1)]);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("criterios[0].parametros.areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
            ("criterios[1].parametros.etapaRef", "ProcessoSeletivo.EtapaRefDesempateInexistente"),
            ("criterios[2].ordem", "ProcessoSeletivo.OrdemDesempateDuplicada"),
        ]);
    }

    [Fact(DisplayName = "Desempate por área sem quadro acumula uma recusa por critério, no campo da regra")]
    public void DefinirCriteriosDesempate_SemQuadroEmDoisCriterios_AcumulaNoCampoDaRegra()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoSemEnem()).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "REDACAO"), PorArea(2, "MATEMATICA")]);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].regra.codigo", "ProcessoSeletivo.DesempateAreaEnemSemQuadro"),
            ("criterios[1].regra.codigo", "ProcessoSeletivo.DesempateAreaEnemSemQuadro"));
    }

    [Fact(DisplayName = "Área que falta em algum grupo do quadro é recusada: o candidato desse grupo ficaria sem a nota")]
    public void DefinirCriteriosDesempate_AreaAusenteEmUmGrupo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoEnem(QuadroComGrupoSemMatematica())).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirCriteriosDesempate([PorArea(1, "MATEMATICA")]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
        result.Error.Message.Should().Contain("Áreas aceitas: LINGUAGENS (Linguagens e suas Tecnologias); REDACAO (Redação).");
    }
}
