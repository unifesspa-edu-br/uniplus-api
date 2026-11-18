namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura da topologia da árvore de satisfação dos documentos exigidos.
/// </summary>
internal static class LeituraDaArvoreDeSatisfacao
{
    /// <summary>
    /// A única chave nova. Cada item é uma raiz; a recursão desce por
    /// <c>filhos</c>, mesmo formato de <see cref="SerializacaoDasExigencias.SerializarNo"/>.
    /// </summary>
    internal static List<NoExigencia> LerArvoreSatisfacao(
        LeitorEnvelope leitor, JsonObject payload, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        JsonArray array = leitor.Array(payload, "arvoreSatisfacao", "$");
        return leitor.Falhou ? [] : LerNos(leitor, array, "arvoreSatisfacao", exigenciasPorId) ?? [];
    }

    /// <summary>
    /// Um nó, recursivamente. <c>tipo</c>/<c>chaveDistincao</c> usam o mesmo <c>FromCodigo</c> das
    /// demais leituras (congelamento dos parâmetros na publicação, UNI-REQ-0019: token não reconhecido é envelope malformado, nunca coerção silenciosa
    /// a um sentinela); <c>repetePorEntidade</c> é o código do grupo, conferido contra os grupos
    /// repostos ao lado da árvore. <c>exigenciaId</c> resolve contra
    /// <paramref name="exigenciasPorId"/> — presente sse <c>tipo</c> é <c>FOLHA</c> (checagem
    /// simétrica à de <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/>,
    /// aqui como forma, não semântica: <see cref="NoExigencia.ReidratarFolha"/> não revalida).
    /// </summary>
    internal static NoExigencia? LerNo(
        LeitorEnvelope leitor, JsonObject item, string path, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        leitor.ExigirChaves(
            item, path,
            "id", "ordem", "tipo", "exigenciaId", "quantidadeMinima", "consequencia", "basesLegais",
            "chaveDistincao", "dataReferencia", "ocorrenciasEsperadas", "repetePorEntidade", "filhos");

        return leitor.Falhou ? null : LerConteudoDoNo(leitor, item, path, exigenciasPorId);
    }

    private static NoExigencia? LerConteudoDoNo(
        LeitorEnvelope leitor, JsonObject item, string path, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        DeclaracaoDoNo d = LerDeclaracao(leitor, item, path);
        if (leitor.Falhou || !LerColecoesDoNo(leitor, item, path, d)
            || !ResolverReferencias(leitor, d, exigenciasPorId, out TipoNo tipo, out ChaveDistincao? chave, out DocumentoExigido? documento))
        {
            return null;
        }

        List<NoExigencia>? filhos = LerFilhos(leitor, item, path, exigenciasPorId);
        return filhos is null || !ConferirFormaDoNo(leitor, d, filhos, tipo)
            ? null
            : ReidratarNo(d, tipo, chave, documento, filhos);
    }

    private static NoExigencia ReidratarNo(
        DeclaracaoDoNo d, TipoNo tipo, ChaveDistincao? chave, DocumentoExigido? documento, IReadOnlyList<NoExigencia> filhos) =>
        tipo == TipoNo.Folha
            ? NoExigencia.ReidratarFolha(d.Id, d.Ordem, documento!, d.QuantidadeMinima,
                DistincaoDasApresentacoes.Reidratar(chave, d.DataReferencia, d.OcorrenciasEsperadas), d.RepetePorEntidadeCodigo)
            : NoExigencia.ReidratarGrupo(d.Id, d.Ordem, new CombinacaoDoGrupo(tipo, d.QuantidadeMinima, d.Consequencia),
                d.BasesLegais, filhos, d.RepetePorEntidadeCodigo);

    private static bool LerColecoesDoNo(LeitorEnvelope leitor, JsonObject item, string path, DeclaracaoDoNo d)
    {
        d.OcorrenciasEsperadas = LerOcorrenciasEsperadasDeNo(leitor, item, path);
        d.BasesLegais = leitor.Falhou ? [] : LerBasesLegaisDeNo(leitor, item, path);
        return !leitor.Falhou;
    }

    private static bool ConferirFormaDoNo(
        LeitorEnvelope leitor, DeclaracaoDoNo d, IReadOnlyList<NoExigencia> filhos, TipoNo tipo)
    {
        string? campo = tipo == TipoNo.Folha ? CampoIncompativelDaFolha(d, filhos) : CampoIncompativelDoGrupo(d);
        return campo is null || RecusarSemRetorno(leitor, campo);
    }

    private static string? CampoIncompativelDaFolha(DeclaracaoDoNo d, IReadOnlyList<NoExigencia> filhos)
    {
        if (d.Consequencia is not null)
        {
            return "A regra de documento não pode ter consequência própria. Remova a consequência.";
        }
        if (d.BasesLegais.Count > 0)
        {
            return "A regra de documento não pode ter base legal própria. Remova a base legal.";
        }
        return filhos.Count > 0 ? "A regra de documento não pode conter outros itens. Remova os itens." : null;
    }

    private static string? CampoIncompativelDoGrupo(DeclaracaoDoNo d)
    {
        if (d.ChaveDistincaoCodigo is not null)
        {
            return "A forma de distinguir apresentações só pode ser informada em uma regra de documento. Remova-a deste item.";
        }
        if (d.DataReferencia is not null)
        {
            return "A data de referência das apresentações só pode ser informada em uma regra de documento. Remova-a deste item.";
        }
        return d.OcorrenciasEsperadas is not null ? "As ocorrências esperadas só podem ser informadas em uma regra de documento. Remova-as deste item." : null;
    }

    private static DeclaracaoDoNo LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string path) => new()
    {
        Id = leitor.Identificador(item, "id", path),
        Ordem = leitor.Inteiro(item, "ordem", path),
        TipoCodigo = leitor.TextoNaoVazio(item, "tipo", path),
        ExigenciaId = leitor.IdentificadorOpcional(item, "exigenciaId", path),
        QuantidadeMinima = leitor.InteiroOpcional(item, "quantidadeMinima", path),
        Consequencia = leitor.TextoOpcional(item, "consequencia", path, LimitesDoEnvelope.Token),
        ChaveDistincaoCodigo = leitor.TextoOpcional(item, "chaveDistincao", path),
        DataReferencia = leitor.DataOpcional(item, "dataReferencia", path),
        RepetePorEntidadeCodigo = leitor.TextoOpcional(item, "repetePorEntidade", path),
    };

    private static bool ResolverReferencias(
        LeitorEnvelope leitor,
        DeclaracaoDoNo d,
        IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId,
        out TipoNo tipo,
        out ChaveDistincao? chave,
        out DocumentoExigido? documento)
    {
        documento = null;
        chave = null;
        tipo = TipoNoCodigo.FromCodigo(d.TipoCodigo);
        if (tipo == TipoNo.Nenhum)
        {
            leitor.Recusar<object>("O tipo de regra informado não é reconhecido. Corrija o tipo informado.");
            return false;
        }

        return ResolverChaveDeDistincao(leitor, d.ChaveDistincaoCodigo, out chave)
            && ResolverDocumento(leitor, tipo, d, exigenciasPorId, out documento);
    }

    private static bool ResolverChaveDeDistincao(LeitorEnvelope leitor, string? codigo, out ChaveDistincao? chave)
    {
        chave = null;
        if (codigo is null)
        {
            return true;
        }

        chave = ChaveDistincaoCodigo.FromCodigo(codigo);
        if (chave != Domain.Enums.ChaveDistincao.Nenhuma)
        {
            return true;
        }

        leitor.Recusar<object>("A forma de distinguir apresentações não é reconhecida. Corrija a forma de distinção na regra de documento.");
        return false;
    }

    private static bool ResolverDocumento(
        LeitorEnvelope leitor,
        TipoNo tipo,
        DeclaracaoDoNo d,
        IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId,
        out DocumentoExigido? documento)
    {
        documento = null;
        if (tipo != TipoNo.Folha)
        {
            return d.ExigenciaId is null || RecusarSemRetorno(leitor, "A composição de regras não pode indicar um documento. Remova o documento deste item.");
        }

        return ResolverDocumentoDaFolha(leitor, d.ExigenciaId, exigenciasPorId, out documento);
    }

    private static bool ResolverDocumentoDaFolha(
        LeitorEnvelope leitor, Guid? exigenciaId,
        IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId, out DocumentoExigido? documento)
    {
        documento = null;
        if (exigenciaId is not { } idDaExigencia)
        {
            return RecusarSemRetorno(leitor, "A regra de documento precisa indicar um documento. Informe o documento exigido.");
        }

        return exigenciasPorId.TryGetValue(idDaExigencia, out documento)
            || RecusarSemRetorno(
                leitor,
                "O documento indicado na regra não consta entre os documentos exigidos. Informe um documento exigido.");
    }

    private static bool RecusarSemRetorno(LeitorEnvelope leitor, string mensagem)
    {
        leitor.Recusar<object>(mensagem);
        return false;
    }

    private static List<NoExigencia>? LerFilhos(
        LeitorEnvelope leitor, JsonObject item, string path, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        JsonArray filhos = leitor.Array(item, "filhos", path);
        return leitor.Falhou ? null : LerNos(leitor, filhos, $"{path}.filhos", exigenciasPorId);
    }

    private static List<NoExigencia>? LerNos(
        LeitorEnvelope leitor, JsonArray array, string path, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        List<NoExigencia> nos = [];
        for (int i = 0; i < array.Count; i++)
        {
            JsonObject item = leitor.ItemObjeto(array, i, path);
            NoExigencia? no = LerNo(leitor, item, $"{path}[{i}]", exigenciasPorId);
            if (leitor.Falhou)
            {
                return null;
            }
            nos.Add(no!);
        }

        return nos;
    }

    private sealed class DeclaracaoDoNo
    {
        public Guid Id { get; init; }

        public int Ordem { get; init; }

        public string TipoCodigo { get; init; } = string.Empty;

        public Guid? ExigenciaId { get; init; }

        public int? QuantidadeMinima { get; init; }

        public string? Consequencia { get; init; }

        public string? ChaveDistincaoCodigo { get; init; }

        public DateOnly? DataReferencia { get; init; }

        public string? RepetePorEntidadeCodigo { get; init; }

        public IReadOnlyList<string>? OcorrenciasEsperadas { get; set; }

        public IReadOnlyList<NoExigenciaBaseLegal> BasesLegais { get; set; } = [];
    }

    /// <summary>Mesma técnica de <c>LeituraDosMetadadosDosFatos.LerValoresDominio</c> para o campo nulo-ou-array.</summary>
    internal static IReadOnlyList<string>? LerOcorrenciasEsperadasDeNo(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.ocorrenciasEsperadas";
        if (item["ocorrenciasEsperadas"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray)
        {
            return leitor.Propagar<IReadOnlyList<string>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array de textos ou null."));
        }

        return leitor.Textos(item, "ocorrenciasEsperadas", pathPai);
    }

    /// <summary>
    /// Base legal PRÓPRIA de um grupo — mesmo formato de <c>LeituraDosFormatosEBasesLegais.LerBasesLegais</c>
    /// (a de <see cref="DocumentoExigido"/>), tipo diferente (<see cref="NoExigenciaBaseLegal"/>).
    /// Só <c>RESOLVIDO</c> é congelado — mesma razão de <c>LeituraDosFormatosEBasesLegais.LerBasesLegais</c>.
    /// </summary>
    internal static IReadOnlyList<NoExigenciaBaseLegal> LerBasesLegaisDeNo(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonArray array = leitor.Array(item, "basesLegais", pathPai);
        return leitor.Falhou ? [] : LerBasesLegaisDoArray(leitor, array, $"{pathPai}.basesLegais");
    }

    private static List<NoExigenciaBaseLegal> LerBasesLegaisDoArray(LeitorEnvelope leitor, JsonArray array, string path)
    {
        List<NoExigenciaBaseLegal> basesLegais = [];
        for (int i = 0; i < array.Count; i++)
        {
            NoExigenciaBaseLegal? baseLegal = LerBaseLegalDoNo(
                leitor, leitor.ItemObjeto(array, i, path), $"{path}[{i}]");
            if (baseLegal is null)
            {
                return [];
            }

            basesLegais.Add(baseLegal);
        }

        return basesLegais;
    }

    private static NoExigenciaBaseLegal? LerBaseLegalDoNo(LeitorEnvelope leitor, JsonObject baseItem, string path)
    {
        if (LeituraDosFormatosEBasesLegais.LerBaseLegalResolvida(leitor, baseItem, path) is not { } lida)
        {
            return null;
        }

        Result<NoExigenciaBaseLegal> baseLegal = NoExigenciaBaseLegal.Criar(lida.Referencia, lida.Abrangencia, lida.Status, lida.Observacao);
        return baseLegal.IsFailure ? leitor.Propagar<NoExigenciaBaseLegal?>(baseLegal.Error!) : baseLegal.Value;
    }
}
