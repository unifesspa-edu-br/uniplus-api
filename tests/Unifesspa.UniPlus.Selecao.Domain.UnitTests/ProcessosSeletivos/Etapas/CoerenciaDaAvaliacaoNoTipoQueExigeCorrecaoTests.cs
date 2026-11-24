namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Xunit;

/// <summary>A avaliação declarada contra o tipo de etapa que exige correção independente, isto é, mais de um corretor por candidato.</summary>
public sealed class CoerenciaDaAvaliacaoNoTipoQueExigeCorrecaoTests
{
    private static List<FieldError> Validar(FormaDeAvaliacao? forma, string? regra, bool exigeCorrecaoIndependente = true, bool notaDeOrigemNoEnem = false) =>
        CoerenciaDaAvaliacaoDaEtapa.Validar(new AvaliacaoParaCoerencia(
            notaDeOrigemNoEnem, forma, regra, ExigeCorrecaoIndependente: exigeCorrecaoIndependente));

    [Fact(DisplayName = "Forma objetiva em tipo que exige correção independente é recusada no campo da forma")]
    public void FormaObjetiva_Recusa()
    {
        List<FieldError> erros = Validar(FormaDeAvaliacao.Objetiva, regra: null);

        erros.Should().ContainSingle();
        erros[0].Field.Should().Be("formaDeAvaliacao");
        erros[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.FormaObjetivaEmTipoQueExigeCorrecaoIndependente);
    }

    [Fact(DisplayName = "A avaliação única em tipo que exige correção independente não é recusada na gravação: o número de corretores é conferido ao congelar")]
    public void AvaliacaoUnica_NaoRecusaNaGravacao() =>
        Validar(FormaDeAvaliacao.Humana, RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo()).Should().BeEmpty();

    [Theory(DisplayName = "Regra de consolidação de mais de um corretor é aceita em tipo que exige correção independente")]
    [InlineData("APURACAO_CORRECAO_INDEPENDENTE")]
    [InlineData("APURACAO_CONCEITO_UNANIMIDADE")]
    [InlineData("APURACAO_CONCEITO_MAIORIA_SIMPLES")]
    [InlineData("APURACAO_CONCEITO_MAIORIA_ABSOLUTA")]
    public void RegraDeMaisDeUmCorretor_Aceita(string regra) =>
        Validar(FormaDeAvaliacao.Humana, regra).Should().BeEmpty();

    [Fact(DisplayName = "Etapa humana sem regra ainda não declarada é tolerada em rascunho: a falta é cobrada na publicação")]
    public void HumanaSemRegra_ToleradaEmRascunho() =>
        Validar(FormaDeAvaliacao.Humana, regra: null).Should().BeEmpty();

    [Fact(DisplayName = "Etapa sem forma declarada é tolerada em rascunho")]
    public void SemForma_ToleradaEmRascunho() =>
        Validar(forma: null, regra: null).Should().BeEmpty();

    [Fact(DisplayName = "Tipo que não exige correção independente aceita forma objetiva e avaliação única")]
    public void TipoQueNaoExige_AceitaObjetivaEAvaliacaoUnica()
    {
        Validar(FormaDeAvaliacao.Objetiva, regra: null, exigeCorrecaoIndependente: false).Should().BeEmpty();
        Validar(FormaDeAvaliacao.Humana, RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), exigeCorrecaoIndependente: false).Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa só a forma declarada, sem repetir a recusa do tipo que exige correção")]
    public void EtapaDoEnem_NaoRepeteARecusaDoTipo()
    {
        List<FieldError> erros = Validar(FormaDeAvaliacao.Objetiva, regra: null, notaDeOrigemNoEnem: true);

        erros.Select(static e => e.Error.Code).Should().Equal(EtapaProcessoErrorCodes.AvaliacaoNaEtapaDoEnem);
    }

    [Fact(DisplayName = "A etapa humana de correção independente sem conceito ainda não é recusada: a ligação do conceito à etapa não existe")]
    public void ExigeCorrecaoSemConceito_AindaNaoEhConferido() =>
        Validar(FormaDeAvaliacao.Humana, RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo()).Should().BeEmpty(
            "a coerência entre exigir correção independente e a etapa ter conceito depende de a etapa carregar o conceito");
}
