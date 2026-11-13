namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A ordem em que <see cref="ProcessoSeletivo.DefinirEtapas"/> recusa: quando o conjunto viola duas
/// regras ao mesmo tempo, a recusa devolvida é a da primeira. Cada teste monta um conjunto que viola
/// as duas regras do par e afirma a primeira; a ordem orienta quem edita a corrigir a causa que
/// invalida as demais.
/// </summary>
public sealed partial class DefinirEtapasPrecedenciaDasRecusasTests
{
    [Fact(DisplayName = "Ordem duplicada vence a etapa de nota do ENEM sob classificação que não é ENEM")]
    public void OrdemDuplicada_VenceACoerenciaComAClassificacao()
    {
        ProcessoSeletivo processo = ComClassificacao(NovoProcesso(), Classificacao(baseadoEmEnem: false));

        Result resultado = processo.DefinirEtapas([EtapaDoEnem(ordem: 1), Etapa("Redação", ordem: 1)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.OrdemEtapaDuplicada");
        resultado.Error!.Message.Should().Be("Cada etapa deve ter uma ordem única dentro do processo.");
    }

    [Fact(DisplayName = "Ordem duplicada vence a regra de nenhuma etapa compor a nota")]
    public void OrdemDuplicada_VenceNenhumaEtapaComponeNota()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirEtapas([EtapaSemNota("Entrevista", ordem: 1), EtapaSemNota("Análise", ordem: 1)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "Sem classificação gravada, a coerência com ela não é conferida e a duplicidade da etapa do ENEM é a recusa")]
    public void SemClassificacao_AEtapaDoEnemSegueParaADuplicidade()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirEtapas([EtapaDoEnem(ordem: 1), EtapaDoEnem(ordem: 2)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaNotaEnemDuplicada");
    }

    [Fact(DisplayName = "A etapa do ENEM fora da média vence a regra de nenhuma etapa compor a nota")]
    public void EtapaDoEnemForaDaMedia_VenceNenhumaEtapaComponeNota()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso foraDaMedia = EtapaProcesso.Reidratar(
            Guid.CreateVersion7(), "Nota do ENEM", CaraterEtapa.Eliminatoria, Tipo(notaDoEnem: true), null, null, 1);

        Result resultado = processo.DefinirEtapas([foraDaMedia, EtapaSemNota("Entrevista", ordem: 2)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
    }

    [Fact(DisplayName = "A etapa do ENEM com banca vence o desempate que cita a etapa removida")]
    public void EtapaDoEnemComBanca_VenceODesempateOrfao()
    {
        EtapaProcesso removida = Etapa("Redação", ordem: 1);
        ProcessoSeletivo processo = ComDesempatePelaNotaDa(ComEtapas(NovoProcesso(), removida), removida);

        Result resultado = processo.DefinirEtapas([EtapaDoEnemComBanca(ordem: 1)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaNotaEnemComBanca");
    }

    [Fact(DisplayName = "Nenhuma etapa compor a nota vence o desempate que cita a etapa removida")]
    public void NenhumaEtapaComponeNota_VenceODesempateOrfao()
    {
        EtapaProcesso removida = Etapa("Redação", ordem: 1);
        ProcessoSeletivo processo = ComDesempatePelaNotaDa(ComEtapas(NovoProcesso(), removida), removida);

        Result resultado = processo.DefinirEtapas([EtapaSemNota("Entrevista", ordem: 1)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    [Fact(DisplayName = "O desempate que cita a etapa removida vence a classificação que cita outra etapa removida")]
    public void DesempateOrfao_VenceAClassificacaoOrfa()
    {
        EtapaProcesso citadaPeloDesempate = Etapa("Redação", ordem: 1);
        EtapaProcesso citadaPelaClassificacao = Etapa("Objetiva", ordem: 2);
        ProcessoSeletivo processo = ComEtapas(NovoProcesso(), citadaPeloDesempate, citadaPelaClassificacao);
        ComDesempatePelaNotaDa(processo, citadaPeloDesempate);
        ComClassificacao(processo, Classificacao(baseadoEmEnem: false, AusenciaNaEtapa(citadaPelaClassificacao.Id)));

        Result resultado = processo.DefinirEtapas([Etapa("Entrevista", ordem: 3)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaReferenciadaPorDesempate");
        resultado.Error!.Message.Should().Contain(citadaPeloDesempate.Id.ToString());
    }

    [Fact(DisplayName = "A eliminação que cita a etapa removida vence a ausência na etapa que passou a ser a do ENEM, mesmo vindo depois dela")]
    public void EliminacaoOrfa_VenceAAusenciaNaEtapaDoEnem()
    {
        EtapaProcesso quePassaAEnem = Etapa("Prova", ordem: 1);
        EtapaProcesso removida = Etapa("Entrevista", ordem: 2);
        ProcessoSeletivo processo = ComEtapas(NovoProcesso(), quePassaAEnem, removida);
        ComClassificacao(processo, Classificacao(baseadoEmEnem: true, AusenciaNaEtapa(quePassaAEnem.Id), AusenciaNaEtapa(removida.Id)));

        Result resultado = processo.DefinirEtapas([ComoEtapaDoEnem(quePassaAEnem)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaReferenciadaPorClassificacao");
        resultado.Error!.Message.Should().Contain(removida.Id.ToString());
    }

    [Fact(DisplayName = "A ausência na etapa que passou a ser a do ENEM vence a exigência documental que cita a etapa removida")]
    public void AusenciaNaEtapaDoEnem_VenceAExigenciaOrfa()
    {
        FaseCronograma fase = Fase(1, Habilitacao);
        EtapaProcesso quePassaAEnem = Etapa("Prova", ordem: 1, Habilitacao);
        EtapaProcesso removida = Etapa("Comprovação de renda", ordem: 2, Habilitacao);
        ProcessoSeletivo processo = ComEtapas(ComCronograma(NovoProcesso(), fase), quePassaAEnem, removida);
        ComClassificacao(processo, Classificacao(baseadoEmEnem: true, AusenciaNaEtapa(quePassaAEnem.Id)));
        ComExigenciasNasEtapas(processo, fase, removida);

        Result resultado = processo.DefinirEtapas([ComoEtapaDoEnem(quePassaAEnem)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.AusenciaNaEtapaDoEnem");
    }

    [Fact(DisplayName = "A exigência que cita a etapa removida vence a exigência cuja etapa mudou de fase, mesmo vindo depois dela")]
    public void ExigenciaOrfa_VenceAEtapaQueMudouDeFase()
    {
        FaseCronograma habilitacao = Fase(1, Habilitacao);
        EtapaProcesso quePassaDeFase = Etapa("Cadastro", ordem: 1, Habilitacao);
        EtapaProcesso removida = Etapa("Comprovação de renda", ordem: 2, Habilitacao);
        ProcessoSeletivo processo = ComEtapas(ComCronograma(NovoProcesso(), habilitacao, Fase(2, Avaliacao)), quePassaDeFase, removida);
        ComExigenciasNasEtapas(processo, habilitacao, quePassaDeFase, removida);

        Result resultado = processo.DefinirEtapas([NaFase(quePassaDeFase, Avaliacao)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaReferenciadaPorExigenciaDocumental");
        resultado.Error!.Message.Should().Contain(removida.Id.ToString());
    }

    [Fact(DisplayName = "A etapa que mudou de fase da exigência vence a etapa que declara fase fora do cronograma, mesmo vindo depois dela")]
    public void EtapaQueMudouDeFase_VenceAFaseForaDoCronograma()
    {
        FaseCronograma habilitacao = Fase(1, Habilitacao);
        EtapaProcesso quePassaDeFase = Etapa("Cadastro", ordem: 1, Habilitacao);
        ProcessoSeletivo processo = ComEtapas(ComCronograma(NovoProcesso(), habilitacao, Fase(2, Avaliacao)), quePassaDeFase);
        ComExigenciasNasEtapas(processo, habilitacao, quePassaDeFase);

        Result resultado = processo.DefinirEtapas([Etapa("Prova", 2, FaseInexistente), NaFase(quePassaDeFase, Avaliacao)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "DocumentoExigido.EtapaNaoPertenceAFase");
    }

    [Fact(DisplayName = "A etapa que declara fase fora do cronograma vence a etapa fora da janela da fase, mesmo vindo depois dela")]
    public void FaseForaDoCronograma_VenceAJanelaDaEtapa()
    {
        ProcessoSeletivo processo = ComCronograma(NovoProcesso(), Fase(1, Avaliacao, "2027-03-01T00:00:00Z", "2027-03-10T00:00:00Z"));
        EtapaProcesso foraDaJanela = EtapaComJanela("Prova objetiva", Avaliacao, "2027-02-20T08:00:00Z", "2027-03-05T08:00:00Z");

        Result resultado = processo.DefinirEtapas([foraDaJanela, Etapa("Entrevista", null, FaseInexistente)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaSemFaseNoCronograma");
        resultado.Error!.Message.Should().Contain(FaseInexistente).And.Contain("Entrevista");
    }

    [Fact(DisplayName = "Na janela da fase, a primeira etapa da lista que viola é a recusa, qualquer que seja o tipo da violação")]
    public void Janela_APrimeiraEtapaDaListaVence()
    {
        ProcessoSeletivo processo = ComCronograma(NovoProcesso(), Fase(1, Avaliacao, "2027-03-01T00:00:00Z", "2027-03-10T00:00:00Z"));
        EtapaProcesso terminaDepois = EtapaComJanela("Prova objetiva", Avaliacao, "2027-03-02T08:00:00Z", "2027-03-11T18:00:00Z");
        EtapaProcesso comecaAntes = EtapaComJanela("Entrevista", Avaliacao, "2027-02-20T08:00:00Z", "2027-03-05T08:00:00Z");

        Result resultado = processo.DefinirEtapas([terminaDepois, comecaAntes], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaTerminaDepoisDaFase");
        resultado.Error!.Message.Should().Contain("Prova objetiva");
    }

    [Fact(DisplayName = "A etapa fora da janela da fase vence o preliminar sem definitivo da mesma etapa")]
    public void Janela_VenceOCicloRecursal()
    {
        ProcessoSeletivo processo = ComCronograma(NovoProcesso(), Fase(1, Avaliacao, "2027-03-01T00:00:00Z", "2027-03-10T00:00:00Z"));
        EtapaProcesso etapa = EtapaComJanela("Prova objetiva", Avaliacao, "2027-02-20T08:00:00Z", "2027-03-05T08:00:00Z");
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaComecaAntesDaFase");
        resultado.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
    }
}
