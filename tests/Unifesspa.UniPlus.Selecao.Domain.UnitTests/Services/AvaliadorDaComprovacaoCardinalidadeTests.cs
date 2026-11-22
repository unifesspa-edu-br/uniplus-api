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
    [Fact(DisplayName = "Regra de documento sem critério de distinção: contagem bruta — 2 apresentações satisfazem N=2")]
    public void Cardinalidade_SemCriterio_ContagemBrutaSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, quantidadeMinima: 2).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, null, null));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "Regra de documento sem critério de distinção: 3 arquivos da mesma competência sem identificação não satisfazem N=2 se só 1 apresentação")]
    public void Cardinalidade_SemCriterio_UmaApresentacaoNaoSatisfazDois()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, quantidadeMinima: 2).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, (string?)null));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Competência mensal: 3 competências regulares corretas satisfaz")]
    public void Cardinalidade_CompetenciaMensal_TresRegularesSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        DateOnly dataReferencia = new(2026, 3, 15);
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 3, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, dataReferencia: dataReferencia).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, "2026-03", "2026-02", "2026-01"));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "Competência mensal: a falta da competência mais recente deixa a regra pendente mesmo com N apresentações")]
    public void Cardinalidade_CompetenciaMensal_FaltaMaisRecentePendente()
    {
        DocumentoExigido documento = DocumentoGeral();
        DateOnly dataReferencia = new(2026, 3, 15);
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 3, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, dataReferencia: dataReferencia).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, "2026-02", "2026-01", "2025-12"));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrência com ocorrências esperadas: cobrir todas as ocorrências esperadas satisfaz")]
    public void Cardinalidade_OcorrenciaComLista_CobreOcorrenciasSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_2026_1", "eleicao_2026_2"));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "Ocorrência com ocorrências esperadas: a falta de uma ocorrência esperada deixa a regra pendente")]
    public void Cardinalidade_OcorrenciaComLista_FaltaOcorrenciaPendente()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_2026_1"));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrência com ocorrências esperadas: a mesma apresentação (mesmo Id) não cobre duas ocorrências ao mesmo tempo")]
    public void Cardinalidade_OcorrenciaComLista_MesmaIdentidadeNaoCobreDuasOcorrencias()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;
        Guid idApresentacao = Guid.CreateVersion7();
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = MesmaApresentacao(documento, idApresentacao, "eleicao_2026_1", "eleicao_2026_2");

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: apresentacoes);

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrência sem ocorrências esperadas: distinção pura por N identificações diferentes satisfaz")]
    public void Cardinalidade_OcorrenciaSemLista_DistincaoPuraSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_x", "eleicao_y"));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "Ocorrência sem ocorrências esperadas: mesma identificação repetida não conta duas vezes")]
    public void Cardinalidade_OcorrenciaSemLista_IdentificacaoRepetidaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_x", "eleicao_x"));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrência sem ocorrências esperadas: identificação nula ou em branco não conta como distinção")]
    public void Cardinalidade_OcorrenciaSemLista_IdentificacaoNulaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesComIdentificacao(documento, "eleicao_x", null, "   "));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Ocorrência sem ocorrências esperadas: a mesma apresentação relatada duas vezes com identificações diferentes não conta como duas")]
    public void Cardinalidade_OcorrenciaSemLista_MesmaIdentidadeComIdentificacoesDiferentesNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(
            documento, 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia).Value!;
        Guid idApresentacao = Guid.CreateVersion7();
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = MesmaApresentacao(documento, idApresentacao, "eleicao_x", "eleicao_y");

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: apresentacoes);

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    [Fact(DisplayName = "Sem critério de distinção: a mesma apresentação relatada duas vezes (mesmo Id) não conta como duas")]
    public void Cardinalidade_SemCriterio_MesmaIdentidadeRepetidaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, quantidadeMinima: 2).Value!;
        ApresentacaoDocumento apresentacao = new(Guid.CreateVersion7());
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = new()
        {
            [documento.Id] = [apresentacao, apresentacao],
        };

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: apresentacoes);

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> MesmaApresentacao(
        DocumentoExigido documento, Guid id, params string[] identificacoes) =>
        new() { [documento.Id] = [.. identificacoes.Select(identificacao => new ApresentacaoDocumento(id, identificacao))] };
}
