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

/// <summary>Os documentos, regras e entradas compartilhados pelos testes da avaliação da comprovação.</summary>
internal static class CenariosDeComprovacao
{
    internal static readonly Guid FaseId = Guid.CreateVersion7();
    internal static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;
    internal static readonly IReadOnlyDictionary<string, FatoResolvido> SemFatos = new Dictionary<string, FatoResolvido>(StringComparer.Ordinal);

    internal static DocumentoExigido DocumentoGeral(string? consequencia = null) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COD", "Nome", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(FaseId, null, FormularioDeTeste.FormularioDaInscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, consequencia, []))
            .ComRequisitos(new RequisitosDoDocumento(Qualquer, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    internal static DocumentoExigido DocumentoCondicional(string fato, string? consequencia = null) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COD", "Nome", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(FaseId, null, FormularioDeTeste.FormularioDaInscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, consequencia, []))
            .ComRequisitos(new RequisitosDoDocumento(Qualquer, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    internal static Dictionary<string, FatoResolvido> Fatos(string fato, bool valor) =>
        new(StringComparer.Ordinal) { [fato] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(valor)) };

    internal static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Apresenta(params DocumentoExigido[] documentos) =>
        documentos.ToDictionary(
            static d => d.Id,
            static IReadOnlyList<ApresentacaoDocumento> (d) => [new ApresentacaoDocumento(Guid.CreateVersion7())]);

    internal static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentacoesComIdentificacao(
        DocumentoExigido documento, params string?[] identificacoes) =>
        new()
        {
            [documento.Id] = [.. identificacoes.Select(static identificacao => new ApresentacaoDocumento(Guid.CreateVersion7(), identificacao))],
        };

    internal static readonly IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> SemApresentacoes =
        new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>();

    internal static RegrasDeComprovacaoDaVersao Regras(params RegraDeComprovacao[] regrasSemGrupo) =>
        RegrasDeComprovacaoDaVersao.DaConfiguracaoPublicada(regrasSemGrupo);

    internal static Result<ResultadoDaComprovacao> Resolver(
        RegrasDeComprovacaoDaVersao regrasDeComprovacao,
        IReadOnlyDictionary<string, FatoResolvido>? fatos = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>>? ocorrencias = null) =>
        AvaliadorDaComprovacao.Resolver(regrasDeComprovacao, fatos ?? SemFatos, apresentacoes ?? SemApresentacoes, ocorrencias);

    internal static OcorrenciaDoGrupo Ocorrencia(string ocorrenciaId, params (string Fato, bool Valor)[] atributos) =>
        new(ocorrenciaId, atributos.ToDictionary(
            static a => a.Fato,
            static a => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(a.Valor)),
            StringComparer.Ordinal));

    internal static Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> OcorrenciasDe(
        string grupo, params OcorrenciaDoGrupo[] ocorrencias) =>
        new(StringComparer.Ordinal) { [grupo] = ocorrencias };

    internal static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentacoesDaOcorrencia(
        DocumentoExigido documento, string ocorrenciaId) =>
        new() { [documento.Id] = [new ApresentacaoDocumento(Guid.CreateVersion7(), OcorrenciaId: ocorrenciaId)] };

    internal static BaseLegalDaComprovacao BaseLegalResolvida() =>
        BaseLegalDaComprovacao.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;
}
