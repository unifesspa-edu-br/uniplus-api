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

/// <summary>Os documentos, árvores e entradas que os testes do resolvedor da árvore de satisfação compartilham.</summary>
internal static class ArvoreDeTeste
{
    internal static readonly Guid FaseId = Guid.CreateVersion7();
    internal static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;
    internal static readonly IReadOnlyDictionary<string, FatoResolvido> SemFatos = new Dictionary<string, FatoResolvido>(StringComparer.Ordinal);

    internal static DocumentoExigido DocumentoGeral(string? consequencia = null) =>
        DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "COD", "Nome", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequencia, [], [], null, Qualquer, null).Value!;

    internal static DocumentoExigido DocumentoCondicional(string fato, string? consequencia = null) =>
        DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "COD", "Nome", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: false, consequencia,
            [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            [], null, Qualquer, null).Value!;

    internal static Dictionary<string, FatoResolvido> Fatos(string fato, bool valor) =>
        new(StringComparer.Ordinal) { [fato] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(valor)) };

    internal static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Apresenta(params DocumentoExigido[] documentos) =>
        documentos.ToDictionary(
            static d => d.Id,
            static IReadOnlyList<ApresentacaoDocumento> (d) => [new ApresentacaoDocumento(Guid.CreateVersion7())]);

    internal static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentaComChaves(
        DocumentoExigido documento, params string?[] chavesDistincao) =>
        new()
        {
            [documento.Id] = [.. chavesDistincao.Select(static chave => new ApresentacaoDocumento(Guid.CreateVersion7(), chave))],
        };

    internal static readonly IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> SemApresentacoes =
        new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>();

    internal static ArvoreExigenciasCongelada Arvore(params NoExigencia[] raizes) =>
        ArvoreExigenciasCongelada.DeGrafoReidratado(raizes);

    internal static Result<ResultadoResolucaoArvore> Resolver(
        ArvoreExigenciasCongelada arvore,
        IReadOnlyDictionary<string, FatoResolvido>? fatos = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null,
        IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>>? instancias = null) =>
        ResolvedorArvoreSatisfacao.Resolver(arvore, fatos ?? SemFatos, apresentacoes ?? SemApresentacoes, instancias);

    internal static InstanciaEntidade Instancia(string entidadeId, params (string Fato, bool Valor)[] atributos) =>
        new(entidadeId, atributos.ToDictionary(
            static a => a.Fato,
            static a => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(a.Valor)),
            StringComparer.Ordinal));

    internal static Dictionary<string, IReadOnlyList<InstanciaEntidade>> InstanciasDe(
        string grupo, params InstanciaEntidade[] instancias) =>
        new(StringComparer.Ordinal) { [grupo] = instancias };

    internal static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentaDeEntidade(
        DocumentoExigido documento, string entidadeId) =>
        new() { [documento.Id] = [new ApresentacaoDocumento(Guid.CreateVersion7(), EntidadeId: entidadeId)] };

    internal static BaseLegalDaComprovacao BaseLegalResolvida() =>
        BaseLegalDaComprovacao.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;
}
