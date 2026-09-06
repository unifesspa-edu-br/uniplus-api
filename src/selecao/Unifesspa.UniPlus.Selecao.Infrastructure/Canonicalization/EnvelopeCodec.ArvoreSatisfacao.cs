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
/// Leitura do bloco <c>arvoreSatisfacao</c> (Story #923): a árvore de nós que agrupa as
/// exigências de <c>documentosExigidos</c> em regras de satisfação.
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// Story #923 — a única chave nova. Cada item é uma raiz; a recursão desce por
    /// <c>filhos</c>, mesmo formato de <c>SnapshotPublicacaoCanonicalizer.SerializarNo</c>.
    /// </summary>
    private static List<NoExigencia> LerArvoreSatisfacao(
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
    /// Um nó, recursivamente. <c>tipo</c>/<c>chaveDistincao</c>/<c>repetePorEntidade</c> usam
    /// o mesmo <c>FromCodigo</c> das demais leituras (RN08: token não reconhecido é envelope
    /// malformado, nunca coerção silenciosa a um sentinela). <c>exigenciaId</c> resolve contra
    /// <paramref name="exigenciasPorId"/> — presente sse <c>tipo</c> é <c>FOLHA</c> (checagem
    /// simétrica à de <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/>,
    /// aqui como forma, não semântica: <see cref="NoExigencia.Reidratar"/> não revalida).
    /// </summary>
    private static NoExigencia? LerNo(
        LeitorEnvelope leitor, JsonObject item, string path, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        leitor.ExigirChaves(
            item, path,
            "id", "ordem", "tipo", "exigenciaId", "quantidadeMinima", "consequencia", "basesLegais",
            "chaveDistincao", "dataReferencia", "ocorrenciasEsperadas", "repetePorEntidade", "filhos");

        Guid id = leitor.Identificador(item, "id", path);
        int ordem = leitor.Inteiro(item, "ordem", path);
        string tipoCodigo = leitor.TextoNaoVazio(item, "tipo", path);
        Guid? exigenciaId = leitor.IdentificadorOpcional(item, "exigenciaId", path);
        int? quantidadeMinima = leitor.InteiroOpcional(item, "quantidadeMinima", path);
        string? consequencia = leitor.TextoOpcional(item, "consequencia", path, LimitesDoEnvelope.Token);
        string? chaveDistincaoCodigo = leitor.TextoOpcional(item, "chaveDistincao", path);
        DateOnly? dataReferencia = leitor.DataOpcional(item, "dataReferencia", path);
        string? repetePorEntidadeCodigo = leitor.TextoOpcional(item, "repetePorEntidade", path);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<string>? ocorrenciasEsperadas = LerOcorrenciasEsperadasDeNo(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<NoExigenciaBaseLegal> basesLegais = LerBasesLegaisDeNo(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        TipoNo tipo = TipoNoCodigo.FromCodigo(tipoCodigo);
        if (tipo == TipoNo.Nenhum)
        {
            return leitor.Propagar<NoExigencia>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.tipo' não reconhecido: '{tipoCodigo}'."));
        }

        ChaveDistincao? chaveDistincao = null;
        if (chaveDistincaoCodigo is not null)
        {
            chaveDistincao = ChaveDistincaoCodigo.FromCodigo(chaveDistincaoCodigo);
            if (chaveDistincao == Domain.Enums.ChaveDistincao.Nenhuma)
            {
                return leitor.Propagar<NoExigencia>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.chaveDistincao' não reconhecida: '{chaveDistincaoCodigo}'."));
            }
        }

        TipoEntidade? repetePorEntidade = null;
        if (repetePorEntidadeCodigo is not null)
        {
            repetePorEntidade = TipoEntidadeCodigo.FromCodigo(repetePorEntidadeCodigo);
            if (repetePorEntidade == TipoEntidade.Nenhuma)
            {
                return leitor.Propagar<NoExigencia>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.repetePorEntidade' não reconhecida: '{repetePorEntidadeCodigo}'."));
            }
        }

        DocumentoExigido? documentoExigido = null;
        if (tipo == TipoNo.Folha)
        {
            if (exigenciaId is not { } idDaExigencia)
            {
                return leitor.Propagar<NoExigencia>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}': nó FOLHA sem 'exigenciaId'."));
            }

            if (!exigenciasPorId.TryGetValue(idDaExigencia, out documentoExigido))
            {
                return leitor.Propagar<NoExigencia>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}.exigenciaId' ({idDaExigencia}) não corresponde a nenhuma exigência em 'documentosExigidos.exigencias'."));
            }
        }
        else if (exigenciaId is not null)
        {
            return leitor.Propagar<NoExigencia>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}': nó '{tipoCodigo}' não pode ter 'exigenciaId'."));
        }

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

        return NoExigencia.Reidratar(
            id, tipo, ordem, exigenciaId, documentoExigido, quantidadeMinima, consequencia,
            chaveDistincao, dataReferencia, ocorrenciasEsperadas, repetePorEntidade, basesLegais, filhos);
    }

    /// <summary>Mesma técnica de <c>LerValoresDominio</c> para o campo nulo-ou-array.</summary>
    private static IReadOnlyList<string>? LerOcorrenciasEsperadasDeNo(LeitorEnvelope leitor, JsonObject item, string pathPai)
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
    /// Base legal PRÓPRIA de um grupo — mesmo formato de <c>LerBasesLegais</c>
    /// (a de <see cref="DocumentoExigido"/>), tipo diferente (<see cref="NoExigenciaBaseLegal"/>).
    /// Só <c>RESOLVIDO</c> é congelado — mesma razão de <c>LerBasesLegais</c>.
    /// </summary>
    private static IReadOnlyList<NoExigenciaBaseLegal> LerBasesLegaisDeNo(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonArray array = leitor.Array(item, "basesLegais", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<NoExigenciaBaseLegal> basesLegais = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.basesLegais[{i}]";
            JsonObject baseItem = leitor.ItemObjeto(array, i, $"{pathPai}.basesLegais");
            leitor.ExigirChaves(baseItem, path, "referencia", "abrangencia", "status", "observacao");

            string referencia = leitor.TextoNaoVazio(baseItem, "referencia", path, LimitesDoEnvelope.BaseLegal);
            string abrangenciaCodigo = leitor.TextoNaoVazio(baseItem, "abrangencia", path);
            string statusCodigo = leitor.TextoNaoVazio(baseItem, "status", path);
            string? observacao = leitor.TextoOpcional(baseItem, "observacao", path, LimitesDoEnvelope.ObservacaoBaseLegal);
            if (leitor.Falhou)
            {
                return [];
            }

            StatusBaseLegal status = StatusBaseLegalCodigo.FromCodigo(statusCodigo);
            if (status != StatusBaseLegal.Resolvido)
            {
                return leitor.Propagar<IReadOnlyList<NoExigenciaBaseLegal>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}.status' deveria ser sempre RESOLVIDO — encontrado '{statusCodigo}'.")) ?? [];
            }

            Result<NoExigenciaBaseLegal> baseLegalResult = NoExigenciaBaseLegal.Criar(
                referencia, TipoAbrangenciaCodigo.FromCodigo(abrangenciaCodigo), status, observacao);
            if (baseLegalResult.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<NoExigenciaBaseLegal>>(baseLegalResult.Error!) ?? [];
            }

            basesLegais.Add(baseLegalResult.Value!);
        }

        return basesLegais;
    }
}
