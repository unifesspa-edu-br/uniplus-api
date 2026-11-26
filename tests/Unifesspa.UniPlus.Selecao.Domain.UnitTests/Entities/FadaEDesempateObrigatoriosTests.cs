namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
/// O FADA é declarado (aplica ou não aplica, como a taxa de inscrição) e o desempate é
/// obrigatório com inscrição própria; com resultado importado a lista já vem classificada.
/// </summary>
public sealed class FadaEDesempateObrigatoriosTests
{
    private static IEnumerable<string> Vermelhos(ProcessoSeletivo processo) => ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
        .Where(static i => !i.Ok)
        .Select(static i => i.Codigo);

    private static ConfiguracaoDoFada Fada() =>
        ConfiguracaoDoFada.Criar(ReferenciaRegra.De(RegraDoFada.Multiplicativo.Codigo(), "v1", new string('a', 64)), 1.20m, null, FormularioDeTeste.FatoBooleanoDaInscricao, new BaseLegalCongelada(Guid.NewGuid(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!;

    [Fact(DisplayName = "FADA por declarar é a única pendência, e declarar que não aplica a resolve")]
    public void FadaNaoDeclarado_ApontaOItem_ENaoAplicaLibera()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararFada: false);

        Vermelhos(processo).Should().Equal("fada_nao_declarado");
        Vermelhos(processo).Should().NotContain("criterios_desempate_ausentes");

        processo.DefinirFada(aplica: false, configuracao: null).IsSuccess.Should().BeTrue();

        Vermelhos(processo).Should().BeEmpty();
        processo.AplicaFada.Should().BeFalse();
    }

    [Fact(DisplayName = "Aplicar o FADA exige a configuração, e não aplicar a recusa")]
    public void Fada_DeclaracaoEConfiguracaoNaoDivergem()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararFada: false);

        processo.DefinirFada(aplica: true, configuracao: null)
            .Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.FadaAplicaSemConfiguracao);
        processo.DefinirFada(aplica: false, configuracao: Fada())
            .Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.FadaNaoAplicaComConfiguracao);
        processo.AplicaFada.Should().BeNull("a recusa não declara nada");

        processo.DefinirFada(aplica: true, Fada()).IsSuccess.Should().BeTrue();
        Vermelhos(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Declarar que não aplica o FADA remove a configuração que havia")]
    public void NaoAplica_RemoveAConfiguracaoAnterior()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararFada: false);
        processo.DefinirFada(aplica: true, Fada()).IsSuccess.Should().BeTrue();

        processo.DefinirFada(aplica: false, configuracao: null).IsSuccess.Should().BeTrue();

        processo.Fada.Should().BeNull();
        processo.AplicaFada.Should().BeFalse();
    }

    [Fact(DisplayName = "Inscrição própria sem critério de desempate é a única pendência")]
    public void InscricaoPropria_SemDesempate_ApontaOItem()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(comDesempate: false);

        processo.OrigemCandidatos.Should().Be(OrigemCandidatos.InscricaoPropria);
        Vermelhos(processo).Should().Equal("criterios_desempate_ausentes");
        Vermelhos(processo).Should().NotContain("fada_nao_declarado");
    }

    [Fact(DisplayName = "Resultado importado dispensa o desempate: a lista já vem classificada")]
    public void ResultadoImportado_DispensaDesempate()
    {
        ProcessoSeletivo processo = ApoioConformidadePublicabilidadeEstrutural.ProcessoConforme();

        processo.OrigemCandidatos.Should().Be(OrigemCandidatos.ImportacaoExterna);
        processo.CriteriosDesempate.Should().BeEmpty();
        Vermelhos(processo).Should().NotContain("criterios_desempate_ausentes");
    }
}
