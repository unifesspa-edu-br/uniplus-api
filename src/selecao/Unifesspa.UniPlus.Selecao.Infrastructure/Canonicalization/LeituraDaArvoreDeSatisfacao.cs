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
        if (leitor.Falhou)
        {
            return [];
        }

        List<NoExigencia> raizes = [];
        for (int i = 0; i < array.Count; i++)
        {
            JsonObject item = leitor.ItemObjeto(array, i, "arvoreSatisfacao");
            NoExigencia? no = LerNo(leitor, item, $"arvoreSatisfacao[{i}]", exigenciasPorId);
            if (leitor.Falhou)
            {
                return [];
            }

            raizes.Add(no!);
        }

        return raizes;
    }

    /// <summary>
    /// Um nó, recursivamente. <c>tipo</c>/<c>chaveDistincao</c> usam o mesmo <c>FromCodigo</c> das
    /// demais leituras (congelamento dos parâmetros na publicação, UNI-REQ-0019: token não reconhecido é envelope malformado, nunca coerção silenciosa
    /// a um sentinela); <c>repetePorEntidade</c> é o código do grupo, conferido contra os grupos
    /// repostos ao lado da árvore. <c>exigenciaId</c> resolve contra
    /// <paramref name="exigenciasPorId"/> — presente sse <c>tipo</c> é <c>FOLHA</c> (checagem
    /// simétrica à de <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/>,
    /// aqui como forma, não semântica: <see cref="NoExigencia.Reidratar"/> não revalida).
    /// </summary>
    internal static NoExigencia? LerNo(
        LeitorEnvelope leitor, JsonObject item, string path, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        leitor.ExigirChaves(
            item, path,
            "id", "ordem", "tipo", "exigenciaId", "quantidadeMinima", "consequencia", "basesLegais",
            "chaveDistincao", "dataReferencia", "ocorrenciasEsperadas", "repetePorEntidade", "filhos");

        DeclaracaoDoNo d = LerDeclaracao(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<string>? ocorrenciasEsperadas = LerOcorrenciasEsperadasDeNo(leitor, item, path);
        IReadOnlyList<NoExigenciaBaseLegal> basesLegais = leitor.Falhou ? [] : LerBasesLegaisDeNo(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (!ResolverReferencias(leitor, d, path, exigenciasPorId, out TipoNo tipo, out ChaveDistincao? chaveDistincao, out DocumentoExigido? documentoExigido))
        {
            return null;
        }

        List<NoExigencia>? filhos = LerFilhos(leitor, item, path, exigenciasPorId);
        return filhos is null
            ? null
            : NoExigencia.Reidratar(
                d.Id, tipo, d.Ordem, d.ExigenciaId, documentoExigido, d.QuantidadeMinima, d.Consequencia,
                chaveDistincao, d.DataReferencia, ocorrenciasEsperadas, d.RepetePorEntidadeCodigo, basesLegais, filhos);
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
        string path,
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
            leitor.Recusar<object>($"'{path}.tipo' não reconhecido: '{d.TipoCodigo}'.");
            return false;
        }

        return ResolverChaveDeDistincao(leitor, d.ChaveDistincaoCodigo, path, out chave)
            && ResolverDocumento(leitor, tipo, d, path, exigenciasPorId, out documento);
    }

    private static bool ResolverChaveDeDistincao(LeitorEnvelope leitor, string? codigo, string path, out ChaveDistincao? chave)
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

        leitor.Recusar<object>($"'{path}.chaveDistincao' não reconhecida: '{codigo}'.");
        return false;
    }

    private static bool ResolverDocumento(
        LeitorEnvelope leitor,
        TipoNo tipo,
        DeclaracaoDoNo d,
        string path,
        IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId,
        out DocumentoExigido? documento)
    {
        documento = null;
        if (tipo != TipoNo.Folha)
        {
            return d.ExigenciaId is null || RecusarSemRetorno(leitor, $"'{path}': nó '{d.TipoCodigo}' não pode ter 'exigenciaId'.");
        }

        if (d.ExigenciaId is not { } idDaExigencia)
        {
            return RecusarSemRetorno(leitor, $"'{path}': nó FOLHA sem 'exigenciaId'.");
        }

        return exigenciasPorId.TryGetValue(idDaExigencia, out documento)
            || RecusarSemRetorno(
                leitor,
                $"'{path}.exigenciaId' ({idDaExigencia}) não corresponde a nenhuma exigência em 'documentosExigidos.exigencias'.");
    }

    private static bool RecusarSemRetorno(LeitorEnvelope leitor, string mensagem)
    {
        leitor.Recusar<object>(mensagem);
        return false;
    }

    private static List<NoExigencia>? LerFilhos(
        LeitorEnvelope leitor, JsonObject item, string path, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        JsonArray filhosArray = leitor.Array(item, "filhos", path);
        if (leitor.Falhou)
        {
            return null;
        }

        List<NoExigencia> filhos = [];
        for (int i = 0; i < filhosArray.Count; i++)
        {
            JsonObject filhoItem = leitor.ItemObjeto(filhosArray, i, $"{path}.filhos");
            NoExigencia? filho = LerNo(leitor, filhoItem, $"{path}.filhos[{i}]", exigenciasPorId);
            if (leitor.Falhou)
            {
                return null;
            }

            filhos.Add(filho!);
        }

        return filhos;
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
        if (leitor.Falhou)
        {
            return [];
        }

        List<NoExigenciaBaseLegal> basesLegais = [];
        for (int i = 0; i < array.Count; i++)
        {
            NoExigenciaBaseLegal? baseLegal = LerBaseLegalDoNo(
                leitor, leitor.ItemObjeto(array, i, $"{pathPai}.basesLegais"), $"{pathPai}.basesLegais[{i}]");
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
