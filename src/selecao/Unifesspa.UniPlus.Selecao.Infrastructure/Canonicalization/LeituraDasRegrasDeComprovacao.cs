namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class LeituraDasRegrasDeComprovacao
{
    internal static List<RegraDeComprovacao> LerRegrasDeComprovacao(
        LeitorEnvelope leitor, JsonObject payload, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        JsonArray array = leitor.Array(payload, "regrasDeComprovacao", "$");
        return leitor.Falhou ? [] : LerRegras(leitor, array, "regrasDeComprovacao", exigenciasPorId) ?? [];
    }

    internal static RegraDeComprovacao? LerRegra(
        LeitorEnvelope leitor, JsonObject item, string caminho, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        leitor.ExigirChaves(
            item, caminho,
            "id", "ordem", "forma", "exigenciaId", "quantidadeMinima", "consequencia", "basesLegais",
            "criterioDeDistincao", "dataReferencia", "ocorrenciasEsperadas", "grupoDoFormularioParaRepeticao", "itens");

        return leitor.Falhou ? null : LerConteudoDaRegra(leitor, item, caminho, exigenciasPorId);
    }

    private static RegraDeComprovacao? LerConteudoDaRegra(
        LeitorEnvelope leitor, JsonObject item, string caminho, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        DeclaracaoDaRegraLida declaracao = LerDeclaracao(leitor, item, caminho);
        if (leitor.Falhou || !LerColecoesDaRegra(leitor, item, caminho, declaracao)
            || !ResolverReferencias(leitor, declaracao, exigenciasPorId, out FormaDeComprovacao forma, out CriterioDeDistincaoDasApresentacoes? criterio, out DocumentoExigido? documento))
        {
            return null;
        }

        List<RegraDeComprovacao>? itens = LerItens(leitor, item, caminho, exigenciasPorId);
        return itens is null || !ConferirFormaDaRegra(leitor, declaracao, itens, forma)
            ? null
            : ReidratarRegra(declaracao, forma, criterio, documento, itens);
    }

    private static RegraDeComprovacao ReidratarRegra(
        DeclaracaoDaRegraLida declaracao, FormaDeComprovacao forma, CriterioDeDistincaoDasApresentacoes? criterio, DocumentoExigido? documento, IReadOnlyList<RegraDeComprovacao> itens) =>
        forma == FormaDeComprovacao.Documento
            ? RegraDeComprovacao.ReidratarRegraDeDocumento(declaracao.Id, declaracao.Ordem, documento!, declaracao.QuantidadeMinima,
                DistincaoDasApresentacoes.Reidratar(criterio, declaracao.DataReferencia, declaracao.OcorrenciasEsperadas), declaracao.GrupoDoFormularioParaRepeticaoCodigo)
            : RegraDeComprovacao.ReidratarGrupo(declaracao.Id, declaracao.Ordem, new CombinacaoDoGrupo(forma, declaracao.QuantidadeMinima, declaracao.Consequencia),
                declaracao.BasesLegais, itens, declaracao.GrupoDoFormularioParaRepeticaoCodigo);

    private static bool LerColecoesDaRegra(LeitorEnvelope leitor, JsonObject item, string caminho, DeclaracaoDaRegraLida declaracao)
    {
        declaracao.OcorrenciasEsperadas = LerOcorrenciasEsperadasDaRegra(leitor, item, caminho);
        declaracao.BasesLegais = leitor.Falhou ? [] : LerBasesLegaisDaRegra(leitor, item, caminho);
        return !leitor.Falhou;
    }

    private static bool ConferirFormaDaRegra(
        LeitorEnvelope leitor, DeclaracaoDaRegraLida declaracao, IReadOnlyList<RegraDeComprovacao> itens, FormaDeComprovacao forma)
    {
        string? campo = forma == FormaDeComprovacao.Documento ? CampoIncompativelDaRegraDeDocumento(declaracao, itens) : CampoIncompativelDoGrupo(declaracao);
        return campo is null || RecusarSemRetorno(leitor, campo);
    }

    private static string? CampoIncompativelDaRegraDeDocumento(DeclaracaoDaRegraLida declaracao, IReadOnlyList<RegraDeComprovacao> itens)
    {
        if (declaracao.Consequencia is not null)
        {
            return "A regra de documento não pode ter consequência própria. Remova a consequência.";
        }
        if (declaracao.BasesLegais.Count > 0)
        {
            return "A regra de documento não pode ter base legal própria. Remova a base legal.";
        }
        return itens.Count > 0 ? "A regra de documento não pode conter outros itens. Remova os itens." : null;
    }

    private static string? CampoIncompativelDoGrupo(DeclaracaoDaRegraLida declaracao)
    {
        if (declaracao.CodigoDoCriterioDeDistincao is not null)
        {
            return "A forma de distinguir apresentações só pode ser informada em uma regra de documento. Remova-a deste item.";
        }
        if (declaracao.DataReferencia is not null)
        {
            return "A data de referência das apresentações só pode ser informada em uma regra de documento. Remova-a deste item.";
        }
        return declaracao.OcorrenciasEsperadas is not null ? "As ocorrências esperadas só podem ser informadas em uma regra de documento. Remova-as deste item." : null;
    }

    private static DeclaracaoDaRegraLida LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string caminho) => new()
    {
        Id = leitor.Identificador(item, "id", caminho),
        Ordem = leitor.Inteiro(item, "ordem", caminho),
        FormaCodigo = leitor.TextoNaoVazio(item, "forma", caminho),
        ExigenciaId = leitor.IdentificadorOpcional(item, "exigenciaId", caminho),
        QuantidadeMinima = leitor.InteiroOpcional(item, "quantidadeMinima", caminho),
        Consequencia = leitor.TextoOpcional(item, "consequencia", caminho, LimitesDoEnvelope.Token),
        CodigoDoCriterioDeDistincao = leitor.TextoOpcional(item, "criterioDeDistincao", caminho),
        DataReferencia = leitor.DataOpcional(item, "dataReferencia", caminho),
        GrupoDoFormularioParaRepeticaoCodigo = leitor.TextoOpcional(item, "grupoDoFormularioParaRepeticao", caminho),
    };

    private static bool ResolverReferencias(
        LeitorEnvelope leitor,
        DeclaracaoDaRegraLida declaracao,
        IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId,
        out FormaDeComprovacao forma,
        out CriterioDeDistincaoDasApresentacoes? criterio,
        out DocumentoExigido? documento)
    {
        documento = null;
        criterio = null;
        if (!CatalogoDe<FormaDeComprovacao>.TentarObter(declaracao.FormaCodigo, out forma))
        {
            leitor.Recusar<object>("A forma de comprovação informada não é reconhecida. Corrija a forma informada.");
            return false;
        }

        return ResolverCriterioDeDistincao(leitor, declaracao.CodigoDoCriterioDeDistincao, out criterio)
            && ResolverDocumento(leitor, forma, declaracao, exigenciasPorId, out documento);
    }

    private static bool ResolverCriterioDeDistincao(LeitorEnvelope leitor, string? codigo, out CriterioDeDistincaoDasApresentacoes? criterio)
    {
        criterio = null;
        if (codigo is null)
        {
            return true;
        }

        if (CatalogoDe<CriterioDeDistincaoDasApresentacoes>.TentarObter(codigo, out CriterioDeDistincaoDasApresentacoes valor))
        {
            criterio = valor;
            return true;
        }

        leitor.Recusar<object>("A forma de distinguir apresentações não é reconhecida. Corrija a forma de distinção na regra de documento.");
        return false;
    }

    private static bool ResolverDocumento(
        LeitorEnvelope leitor,
        FormaDeComprovacao forma,
        DeclaracaoDaRegraLida declaracao,
        IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId,
        out DocumentoExigido? documento)
    {
        documento = null;
        if (forma != FormaDeComprovacao.Documento)
        {
            return declaracao.ExigenciaId is null || RecusarSemRetorno(leitor, "A composição de regras não pode indicar um documento. Remova o documento deste item.");
        }

        return ResolverDocumentoDaRegra(leitor, declaracao.ExigenciaId, exigenciasPorId, out documento);
    }

    private static bool ResolverDocumentoDaRegra(
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

    private static List<RegraDeComprovacao>? LerItens(
        LeitorEnvelope leitor, JsonObject item, string caminho, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        JsonArray itens = leitor.Array(item, "itens", caminho);
        return leitor.Falhou ? null : LerRegras(leitor, itens, $"{caminho}.itens", exigenciasPorId);
    }

    private static List<RegraDeComprovacao>? LerRegras(
        LeitorEnvelope leitor, JsonArray array, string caminho, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        List<RegraDeComprovacao> regras = [];
        for (int indice = 0; indice < array.Count; indice++)
        {
            JsonObject item = leitor.ItemObjeto(array, indice, caminho);
            RegraDeComprovacao? regra = LerRegra(leitor, item, $"{caminho}[{indice}]", exigenciasPorId);
            if (leitor.Falhou)
            {
                return null;
            }
            regras.Add(regra!);
        }

        return regras;
    }

    private sealed class DeclaracaoDaRegraLida
    {
        public Guid Id { get; init; }

        public int Ordem { get; init; }

        public string FormaCodigo { get; init; } = string.Empty;

        public Guid? ExigenciaId { get; init; }

        public int? QuantidadeMinima { get; init; }

        public string? Consequencia { get; init; }

        public string? CodigoDoCriterioDeDistincao { get; init; }

        public DateOnly? DataReferencia { get; init; }

        public string? GrupoDoFormularioParaRepeticaoCodigo { get; init; }

        public IReadOnlyList<string>? OcorrenciasEsperadas { get; set; }

        public IReadOnlyList<BaseLegalDaComprovacao> BasesLegais { get; set; } = [];
    }

    internal static IReadOnlyList<string>? LerOcorrenciasEsperadasDaRegra(LeitorEnvelope leitor, JsonObject item, string caminhoDaRegra)
    {
        string caminho = $"{caminhoDaRegra}.ocorrenciasEsperadas";
        if (item["ocorrenciasEsperadas"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray)
        {
            return leitor.Propagar<IReadOnlyList<string>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{caminho}' deveria ser um array de textos ou null."));
        }

        return leitor.Textos(item, "ocorrenciasEsperadas", caminhoDaRegra);
    }

    internal static IReadOnlyList<BaseLegalDaComprovacao> LerBasesLegaisDaRegra(LeitorEnvelope leitor, JsonObject item, string caminhoDaRegra)
    {
        JsonArray array = leitor.Array(item, "basesLegais", caminhoDaRegra);
        return leitor.Falhou ? [] : LerBasesLegaisDoArray(leitor, array, $"{caminhoDaRegra}.basesLegais");
    }

    private static List<BaseLegalDaComprovacao> LerBasesLegaisDoArray(LeitorEnvelope leitor, JsonArray array, string caminho)
    {
        List<BaseLegalDaComprovacao> basesLegais = [];
        for (int indice = 0; indice < array.Count; indice++)
        {
            BaseLegalDaComprovacao? baseLegal = LerBaseLegalDaRegra(
                leitor, leitor.ItemObjeto(array, indice, caminho), $"{caminho}[{indice}]");
            if (baseLegal is null)
            {
                return [];
            }

            basesLegais.Add(baseLegal);
        }

        return basesLegais;
    }

    private static BaseLegalDaComprovacao? LerBaseLegalDaRegra(LeitorEnvelope leitor, JsonObject baseItem, string caminho)
    {
        if (LeituraDosFormatosEBasesLegais.LerBaseLegalResolvida(leitor, baseItem, caminho) is not { } lida)
        {
            return null;
        }

        Result<BaseLegalDaComprovacao> baseLegal = BaseLegalDaComprovacao.Criar(lida.Referencia, lida.Abrangencia, lida.Situacao, lida.Observacao);
        return baseLegal.IsFailure ? leitor.Propagar<BaseLegalDaComprovacao?>(baseLegal.Errors, caminho) : baseLegal.Value;
    }
}
