namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O bônus regional é declarado (aplica ou não aplica, como a taxa de inscrição) e o desempate é
/// obrigatório com inscrição própria; com resultado importado a lista já vem classificada.
/// </summary>
public sealed class BonusEDesempateObrigatoriosTests
{
    private static IEnumerable<string> Vermelhos(ProcessoSeletivo processo) => ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Padrao)
        .Where(static i => !i.Ok)
        .Select(static i => i.Codigo);

    private static ConfiguracaoBonusRegional Bonus() =>
        ConfiguracaoBonusRegional.Criar(ReferenciaRegra.De(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)), 1.20m, null, FormularioDeTeste.FatoBooleanoDaInscricao, new BaseLegalCongelada(Guid.NewGuid(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!;

    [Fact(DisplayName = "Bônus por declarar é a única pendência, e declarar que não aplica a resolve")]
    public void BonusNaoDeclarado_ApontaOItem_ENaoAplicaLibera()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararBonus: false);

        Vermelhos(processo).Should().Equal("bonus_regional_nao_declarado");
        Vermelhos(processo).Should().NotContain("criterios_desempate_ausentes");

        processo.DefinirBonusRegional(aplica: false, bonus: null).IsSuccess.Should().BeTrue();

        Vermelhos(processo).Should().BeEmpty();
        processo.AplicaBonusRegional.Should().BeFalse();
    }

    [Fact(DisplayName = "Aplicar o bônus exige a configuração, e não aplicar a recusa")]
    public void Bonus_DeclaracaoEConfiguracaoNaoDivergem()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararBonus: false);

        processo.DefinirBonusRegional(aplica: true, bonus: null)
            .Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.BonusRegionalAplicaSemConfiguracao);
        processo.DefinirBonusRegional(aplica: false, bonus: Bonus())
            .Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao);
        processo.AplicaBonusRegional.Should().BeNull("a recusa não declara nada");

        processo.DefinirBonusRegional(aplica: true, Bonus()).IsSuccess.Should().BeTrue();
        Vermelhos(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Declarar que não aplica o bônus remove a configuração que havia")]
    public void NaoAplica_RemoveAConfiguracaoAnterior()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararBonus: false);
        processo.DefinirBonusRegional(aplica: true, Bonus()).IsSuccess.Should().BeTrue();

        processo.DefinirBonusRegional(aplica: false, bonus: null).IsSuccess.Should().BeTrue();

        processo.BonusRegional.Should().BeNull();
        processo.AplicaBonusRegional.Should().BeFalse();
    }

    [Fact(DisplayName = "Bônus gravado antes da declaração (configuração sem declaração) volta como por declarar")]
    public void ConfiguracaoSemDeclaracao_ContinuaPorDeclarar()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararBonus: false);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.BonusRegional))!.SetValue(processo, Bonus());

        Vermelhos(processo).Should().Equal("bonus_regional_nao_declarado");
    }

    [Fact(DisplayName = "Inscrição própria sem critério de desempate é a única pendência")]
    public void InscricaoPropria_SemDesempate_ApontaOItem()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(comDesempate: false);

        processo.OrigemCandidatos.Should().Be(OrigemCandidatos.InscricaoPropria);
        Vermelhos(processo).Should().Equal("criterios_desempate_ausentes");
        Vermelhos(processo).Should().NotContain("bonus_regional_nao_declarado");
    }

    [Fact(DisplayName = "Resultado importado dispensa o desempate: a lista já vem classificada")]
    public void ResultadoImportado_DispensaDesempate()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(comDesempate: false);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.OrigemCandidatos))!
            .SetValue(processo, OrigemCandidatos.ImportacaoExterna);

        Vermelhos(processo).Should().NotContain("criterios_desempate_ausentes");
    }
}
