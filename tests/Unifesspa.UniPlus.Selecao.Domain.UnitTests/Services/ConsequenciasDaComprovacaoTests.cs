namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static CenariosDeComprovacao;

/// <summary>Verifica quais regras determinam as consequências da comprovação.</summary>
public sealed class ConsequenciasDaComprovacaoTests
{
    [Fact(DisplayName = "Grupo cumprido não aplica consequências nem mantém orientações dos itens")]
    public void GrupoCumprido_NaoAplicaConsequenciasDosItens()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        DocumentoExigido cpf = DocumentoGeral("ELIMINA");
        DocumentoExigido cin = DocumentoGeral("RECLASSIFICA_AC");
        RegraDeComprovacao grupoDeTodos = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(cpf, 1).Value!]).Value!;
        RegraDeComprovacao regraDeCin = RegraDeComprovacao.CriarRegraDeDocumento(cin, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [], [grupoDeTodos, regraDeCin]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: Apresenta(cin));

        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
        resultado.Value!.PendenciasDeOrientacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Grupo pendente com consequência própria aplica a consequência do grupo")]
    public void GrupoPendente_AplicaConsequenciaPropria()
    {
        DocumentoExigido a = DocumentoGeral("ELIMINA");
        DocumentoExigido b = DocumentoGeral("ABRE_PRAZO_PARA_REENVIO");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, "RECLASSIFICA_AC", [BaseLegalResolvida()],
            [RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(b, 1).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo));

        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.RegraDeComprovacaoId == regraSemGrupo.Id && c.FormaDaRegra == FormaDeComprovacao.QuantidadeMinimaDeItens && c.Consequencia == "RECLASSIFICA_AC");
        resultado.Value!.PendenciasDeOrientacao.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Grupo que exige todos os itens aplica as consequências dos itens não cumpridos")]
    public void GrupoDeTodos_AplicaConsequenciaDosItensNaoCumpridos()
    {
        DocumentoExigido primeiroDocumento = DocumentoCondicional("FATO_X", "ELIMINA");
        DocumentoExigido segundoDocumento = DocumentoGeral("RECLASSIFICA_AC");
        RegraDeComprovacao primeiraRegra = RegraDeComprovacao.CriarRegraDeDocumento(primeiroDocumento, 0).Value!;
        RegraDeComprovacao segundaRegra = RegraDeComprovacao.CriarRegraDeDocumento(segundoDocumento, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [primeiraRegra, segundaRegra]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: Apresenta(segundoDocumento));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.AguardandoDados);
        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.RegraDeComprovacaoId == primeiraRegra.Id && c.FormaDaRegra == FormaDeComprovacao.Documento && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "Regra de documento sem grupo aplica sua consequência quando pendente")]
    public void RegraDeDocumentoSemGrupoPendente_AplicaConsequencia()
    {
        DocumentoExigido documento = DocumentoGeral("ELIMINA");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo));

        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.RegraDeComprovacaoId == regraSemGrupo.Id && c.FormaDaRegra == FormaDeComprovacao.Documento && c.Consequencia == "ELIMINA");
        resultado.Value!.SituacoesPorExigencia[documento.Id].Should().Be(StatusResolucaoExigencia.PendenteDeComprovacao);
    }
}
