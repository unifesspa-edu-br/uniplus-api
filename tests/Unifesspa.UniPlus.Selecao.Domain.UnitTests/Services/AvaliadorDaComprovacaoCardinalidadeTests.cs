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

using static RegrasDeComprovacaoDeTeste;

public sealed class AvaliadorDaComprovacaoCardinalidadeTests
{
    [Fact(DisplayName = "Folha sem chaveDistincao: contagem bruta — 2 apresentações satisfazem N=2")]
    public void Cardinalidade_SemChave_ContagemBrutaSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, quantidadeMinima: 2).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, null, null));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "Folha sem chaveDistincao: 3 arquivos da mesma competência sem tag não satisfazem N=2 se só 1 apresentação")]
    public void Cardinalidade_SemChave_UmaApresentacaoNaoSatisfazDois()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, quantidadeMinima: 2).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, (string?)null));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "CompetenciaMensal: 3 competências regulares corretas satisfaz")]
    public void Cardinalidade_CompetenciaMensal_TresRegularesSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        DateOnly ancora = new(2026, 3, 15);
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: ancora).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, "2026-03", "2026-02", "2026-01"));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "CompetenciaMensal: falta a competência mais recente fica pendente mesmo com N apresentações")]
    public void Cardinalidade_CompetenciaMensal_FaltaMaisRecentePendente()
    {
        DocumentoExigido documento = DocumentoGeral();
        DateOnly ancora = new(2026, 3, 15);
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: ancora).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, "2026-02", "2026-01", "2025-12"));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrencia com ocorrenciasEsperadas: cobre os slots concretos satisfaz")]
    public void Cardinalidade_OcorrenciaComLista_CobreSlotsSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_2026_1", "eleicao_2026_2"));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "Ocorrencia com ocorrenciasEsperadas: falta um slot concreto fica pendente")]
    public void Cardinalidade_OcorrenciaComLista_FaltaSlotPendente()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_2026_1"));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrencia com ocorrenciasEsperadas: a mesma apresentação (mesmo Id) não cobre dois slots ao mesmo tempo")]
    public void Cardinalidade_OcorrenciaComLista_MesmaIdentidadeNaoCobreDoisSlots()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;
        Guid idApresentacao = Guid.CreateVersion7();
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = MesmaApresentacao(documento, idApresentacao, "eleicao_2026_1", "eleicao_2026_2");

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(raiz), apresentacoes: apresentacoes);

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrencia sem ocorrenciasEsperadas: distinção pura por N tags diferentes satisfaz")]
    public void Cardinalidade_OcorrenciaSemLista_DistincaoPuraSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_x", "eleicao_y"));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "Ocorrencia sem ocorrenciasEsperadas: mesma tag repetida não conta duas vezes")]
    public void Cardinalidade_OcorrenciaSemLista_TagRepetidaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_x", "eleicao_x"));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrencia sem ocorrenciasEsperadas: tag nula/em branco não conta como distinção")]
    public void Cardinalidade_OcorrenciaSemLista_TagNulaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(raiz), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_x", null, "   "));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrencia sem ocorrenciasEsperadas: a mesma apresentação relatada duas vezes com tags diferentes não conta como duas")]
    public void Cardinalidade_OcorrenciaSemLista_MesmaIdentidadeComTagsDiferentesNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia).Value!;
        Guid idApresentacao = Guid.CreateVersion7();
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = MesmaApresentacao(documento, idApresentacao, "eleicao_x", "eleicao_y");

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(raiz), apresentacoes: apresentacoes);

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Sem chaveDistincao: a mesma apresentação relatada duas vezes (mesmo Id) não conta como duas")]
    public void Cardinalidade_SemChave_MesmaIdentidadeRepetidaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao raiz = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, quantidadeMinima: 2).Value!;
        ApresentacaoDocumento apresentacao = new(Guid.CreateVersion7());
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = new()
        {
            [documento.Id] = [apresentacao, apresentacao],
        };

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(raiz), apresentacoes: apresentacoes);

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    // ── Repetição por entidade (Story #922) ──────────────────────────────────────────────

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> MesmaApresentacao(
        DocumentoExigido documento, Guid id, params string[] chaves) =>
        new() { [documento.Id] = [.. chaves.Select(chave => new ApresentacaoDocumento(id, chave))] };
}
