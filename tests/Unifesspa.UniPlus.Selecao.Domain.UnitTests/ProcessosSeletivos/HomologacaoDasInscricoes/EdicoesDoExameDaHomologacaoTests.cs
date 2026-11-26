namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>
/// A edição do exame de cada confirmação do INEP precisa ser uma das edições do ENEM que o processo declara. Sem edições
/// declaradas não há o que comparar, e a regra que a leitura do documento recusou não é julgada.
/// </summary>
public sealed class EdicoesDoExameDaHomologacaoTests
{
    private const string Resultado = "RESULTADO_HOMOLOGACAO";
    private const string Motivo = "SEM_RESULTADO_INEP";
    private const string CampoDaPrimeiraCondicao = "fases[1].regraDeHomologacao.alternativas[0].condicoes[0].edicaoDoExame";

    private static OpcaoDeclaradaFato Edicao(string codigo) => OpcaoDeclaradaFato.Criar("EDICAO_ENEM", codigo, codigo, 0).Value!;

    private static OpcaoDeclaradaFato OpcaoDeOutroFato(string codigo) => OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", codigo, codigo, 0).Value!;

    private static CronogramaDoProcesso Cronograma(params RegraDeHomologacaoDasInscricoes[] regras) =>
        CronogramaDoProcesso.Do([.. regras.Select(static (regra, indice) => FaseHomologando(indice, regra).Adotada())]);

    private static ModeloDeFase FaseHomologando(int indice, RegraDeHomologacaoDasInscricoes regra) =>
        new ModeloDeFase("HOMOLOGACAO_INSCRICOES", indice + 1).Homologando(regra);

    private static RegraDeHomologacaoDasInscricoes Regra(params int[] edicoes) =>
        RegraDeHomologacaoDasInscricoes.Declarar(Resultado, [.. edicoes.Select(static edicao => Alternativa(ConfirmacaoDoInep(edicao, Motivo)))]);

    private static IReadOnlyList<FieldError> Causas(CronogramaDoProcesso cronograma, params OpcaoDeclaradaFato[] opcoes) =>
        EdicoesDoExameDaHomologacao.Causas(cronograma, opcoes);

    [Fact(DisplayName = "A edição da condição que o processo declara é aceita")]
    public void Causas_EdicaoDeclarada_Aceita() =>
        Causas(Cronograma(Regra(2025)), Edicao("2024"), Edicao("2025")).Should().BeEmpty();

    [Fact(DisplayName = "Várias condições com edições diferentes, todas declaradas pelo processo, são aceitas")]
    public void Causas_VariasCondicoesComEdicoesDiferentesTodasDeclaradas_Aceita() =>
        Causas(Cronograma(Regra(2024, 2025)), Edicao("2024"), Edicao("2025")).Should().BeEmpty();

    [Fact(DisplayName = "A edição que o processo não declara é recusada com o código novo, no campo da edição da condição")]
    public void Causas_EdicaoNaoDeclarada_RecusaComOCodigoNovoNoCampoDaCondicao()
    {
        FieldError causa = Causas(Cronograma(Regra(2025), Regra(2023)), Edicao("2024"), Edicao("2025")).Should().ContainSingle().Subject;

        causa.Field.Should().Be(CampoDaPrimeiraCondicao);
        causa.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.EdicaoDoExameNaoDeclarada);
    }

    [Fact(DisplayName = "A mensagem da recusa não cita o ano da condição nem os anos declarados")]
    public void Causas_EdicaoNaoDeclarada_MensagemNaoCitaOAno()
    {
        FieldError causa = Causas(Cronograma(Regra(2023)), Edicao("2025")).Should().ContainSingle().Subject;

        causa.Error.Message.Should().NotContain("2023").And.NotContain("2025");
    }

    [Fact(DisplayName = "Só as condições com edição não declarada são recusadas, cada uma na posição dela")]
    public void Causas_VariasCondicoesComAlgumaNaoDeclarada_RecusaSoAsNaoDeclaradasNaPosicaoDelas()
    {
        IReadOnlyList<FieldError> causas = Causas(Cronograma(Regra(2025, 2023, 2024, 2022)), Edicao("2024"), Edicao("2025"));

        causas.Select(static causa => causa.Field).Should().Equal(
            "fases[0].regraDeHomologacao.alternativas[1].condicoes[0].edicaoDoExame",
            "fases[0].regraDeHomologacao.alternativas[3].condicoes[0].edicaoDoExame");
    }

    [Fact(DisplayName = "A condição recusada dentro de uma alternativa com várias condições aponta a posição dela")]
    public void Causas_CondicaoDeAlternativaComVariasCondicoes_ApontaAPosicaoDaCondicao()
    {
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar(
            Resultado, [Alternativa(ConfirmacaoDoInep(2025, Motivo), ConfirmacaoDoInepSoParaAmpla(2023, Motivo))]);

        Causas(Cronograma(regra), Edicao("2025")).Select(static causa => causa.Field)
            .Should().Equal("fases[0].regraDeHomologacao.alternativas[0].condicoes[1].edicaoDoExame");
    }

    [Fact(DisplayName = "Sem edições declaradas, a edição da condição não é conferida")]
    public void Causas_SemEdicoesDeclaradas_NaoConfereAEdicao() =>
        Causas(Cronograma(Regra(2023))).Should().BeEmpty();

    [Fact(DisplayName = "Opções declaradas só de outro dado não contam como edições declaradas")]
    public void Causas_OpcoesSoDeOutroFato_NaoConfereAEdicao() =>
        Causas(Cronograma(Regra(2023)), OpcaoDeOutroFato("2025")).Should().BeEmpty();

    [Fact(DisplayName = "A opção de outro dado com o ano da condição não a torna declarada")]
    public void Causas_OpcaoDeOutroFatoComOAnoDaCondicao_NaoContaComoEdicaoDeclarada() =>
        Causas(Cronograma(Regra(2023)), Edicao("2025"), OpcaoDeOutroFato("2023"))
            .Should().ContainSingle().Which.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.EdicaoDoExameNaoDeclarada);

    [Fact(DisplayName = "O código de opção que não é o ano de quatro algarismos não casa com a edição da condição")]
    public void Causas_CodigoDaOpcaoQueNaoEOAnoComQuatroAlgarismos_NaoCasaComAEdicao() =>
        Causas(Cronograma(Regra(2025)), Edicao("ENEM_2025"), Edicao("25"))
            .Should().ContainSingle().Which.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.EdicaoDoExameNaoDeclarada);

    [Fact(DisplayName = "A regra sem alternativas não tem edição a conferir")]
    public void Causas_RegraSemAlternativas_NaoTemEdicaoAConferir() =>
        Causas(Cronograma(RegraDeHomologacaoDasInscricoes.Declarar(Resultado, [])), Edicao("2025")).Should().BeEmpty();

    [Fact(DisplayName = "A regra que a leitura do documento recusou não é julgada")]
    public void Causas_RegraRecusadaNaLeitura_NaoJulgaAEdicao()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(
            [FaseHomologando(0, Regra(2023)).Adotada()], ComPartesRecusadas("fases[0].regraDeHomologacao"));

        Causas(cronograma, Edicao("2025")).Should().BeEmpty();
    }
}
