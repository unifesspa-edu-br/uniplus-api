namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed partial class DefinirEtapasPrecedenciaDasRecusasTests
{
    [Fact(DisplayName = "Recusa simples vira falha de validação com um único erro, sem campo, que é o Error do resultado")]
    public void RecusaSimples_UmErroSemCampo()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirEtapas([Etapa("Objetiva", ordem: 1), Etapa("Redação", ordem: 1)], PrecondicaoIfMatch.Ausente);

        FieldError unico = resultado.Errors.Should().ContainSingle().Subject;
        unico.Field.Should().BeNull();
        unico.Error.Should().BeSameAs(resultado.Error);
        unico.Error.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "O ciclo recursal aberto devolve uma violação por etapa e por tipo de ato, na ordem das etapas, no campo produtos da etapa")]
    public void CicloAberto_UmaViolacaoPorEtapaETipo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso[] etapas =
        [
            EtapaComPreliminaresAbertos("Objetiva", 1, "GABARITO", "RESULTADO_PROVA"),
            EtapaComCicloFechado("Redação", 2),
            EtapaComPreliminaresAbertos("Entrevista", 3, "PARECER"),
        ];

        Result resultado = processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().Equal("etapas[0].produtos", "etapas[0].produtos", "etapas[2].produtos");
        resultado.Errors.Should().OnlyContain(e => e.Error.Code == ProcessoSeletivoErrorCodes.EtapaComPreliminarSemDefinitivo);
    }

    [Fact(DisplayName = "Cada violação do ciclo nomeia a etapa e o tipo de ato que ficou sem definitivo")]
    public void CicloAberto_MensagemNomeiaEtapaETipo()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirEtapas(
            [EtapaComPreliminaresAbertos("Objetiva", 1, "GABARITO", "RESULTADO_PROVA")], PrecondicaoIfMatch.Ausente);

        resultado.Errors[0].Error.Message.Should().Contain("'Objetiva'").And.Contain("'GABARITO'").And.NotContain("RESULTADO_PROVA");
        resultado.Errors[1].Error.Message.Should().Contain("'Objetiva'").And.Contain("'RESULTADO_PROVA'");
    }

    [Fact(DisplayName = "Conjunto que viola o ciclo e também uma recusa da coleção devolve só a recusa da coleção")]
    public void CicloAbertoComRecusaDaColecao_DevolveSoARecusaDaColecao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso[] etapas = [EtapaComPreliminaresAbertos("Objetiva", 1, "GABARITO"), Etapa("Redação", ordem: 1)];

        Result resultado = processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.OrdemEtapaDuplicada");
        resultado.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
    }

    [Fact(DisplayName = "Lista vazia é válida: o processo sem prova não tem etapa")]
    public void ListaVazia_EhValida()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirEtapas([], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Errors.Should().BeEmpty();
        processo.Etapas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Lista vazia remove as etapas gravadas, sem acionar a regra de nenhuma etapa compor a nota")]
    public void ListaVazia_RemoveAsEtapasGravadas()
    {
        ProcessoSeletivo processo = ComEtapas(NovoProcesso(), Etapa("Objetiva", ordem: 1));

        Result resultado = processo.DefinirEtapas([], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem classificação gravada, a etapa do ENEM válida é aceita e as demais regras seguem valendo")]
    public void ClassificacaoNula_AceitaAEtapaDoEnemValida()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirEtapas([EtapaDoEnem(ordem: 1), Etapa("Redação", ordem: 2)], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sucesso substitui as etapas pelas informadas, na ordem informada, vinculadas ao processo")]
    public void Sucesso_SubstituiNaOrdemInformadaEVincula()
    {
        ProcessoSeletivo processo = ComEtapas(NovoProcesso(), Etapa("Antiga", ordem: 1));
        EtapaProcesso primeira = Etapa("Redação", ordem: 2);
        EtapaProcesso segunda = Etapa("Objetiva", ordem: 1);

        Result resultado = processo.DefinirEtapas([primeira, segunda], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().HaveCount(2);
        processo.Etapas.ElementAt(0).Should().BeSameAs(primeira);
        processo.Etapas.ElementAt(1).Should().BeSameAs(segunda);
        processo.Etapas.Should().OnlyContain(e => e.ProcessoSeletivoId == processo.Id);
    }
}
