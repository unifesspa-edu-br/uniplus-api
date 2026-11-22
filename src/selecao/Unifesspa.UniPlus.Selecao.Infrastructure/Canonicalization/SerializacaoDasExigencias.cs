namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope os documentos exigidos: exigências, obrigatoriedades, bases legais e regras de comprovação.
/// </summary>
internal static class SerializacaoDasExigencias
{
    internal static JsonObject SerializarDocumentosExigidos(
        ProcessoSeletivo processo,
        ResultadoConformidade? conformidade,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados,
        TimeZoneInfo fusoHorario) => new()
        {
            ["exigencias"] = SerializarExigencias(processo.DocumentosExigidos),
            ["obrigatoriedades"] = SerializarObrigatoriedades(conformidade),
            ["referenciaTemporalFatos"] = processo.ReferenciaTemporalFatos is { } referencia ? new JsonObject
            {
                ["tipo"] = referencia.Tipo.ToCodigo(),
                ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["faseId"] = referencia.FaseId,
            }
            : null,
            ["dataReferenciaFatos"] = Domain.ProcessosSeletivos.Fatos.ReferenciaTemporalDosFatos.ResolverDataReferenciaFatos(processo.Declaracao(), fusoHorario) is { } data
            ? data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null,
            ["metadadosFatos"] = SerializacaoDosFatos.SerializarMetadadosFatos(metadadosFatosCongelados),
        };

    internal static JsonArray SerializarRegrasDeComprovacao(ProcessoSeletivo processo) =>
        new([.. processo.RegrasDeComprovacaoSemGrupo.OrderBy(static r => r.Ordem).Select(static r => (JsonNode)SerializarRegra(r))]);

    private static JsonObject SerializarRegra(RegraDeComprovacao regra) => new()
    {
        ["id"] = regra.Id,
        ["ordem"] = regra.Ordem,
        ["forma"] = regra.Forma.Codigo(),
        ["exigenciaId"] = regra.Forma == FormaDeComprovacao.Documento ? regra.DocumentoExigidoId : null,
        ["quantidadeMinima"] = regra.QuantidadeMinima,
        ["consequencia"] = regra.Consequencia is { } consequencia ? HashCanonicalComputer.NormalizeNfc(consequencia) : null,
        ["basesLegais"] = SerializarBasesLegaisDaRegra(regra.BasesLegaisResolvidas()),
        ["criterioDeDistincao"] = regra.Distincao?.Criterio?.ToCodigo(),
        ["dataReferencia"] = regra.Distincao?.DataReferencia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["ocorrenciasEsperadas"] = SerializarOcorrencias(regra.Distincao),
        ["grupoDoFormularioParaRepeticao"] = regra.GrupoDoFormularioParaRepeticao,
        ["itens"] = new JsonArray([.. regra.Itens.OrderBy(static f => f.Ordem).Select(static f => (JsonNode)SerializarRegra(f))]),
    };

    private static JsonArray? SerializarOcorrencias(DistincaoDasApresentacoes? distincao) =>
        distincao?.OcorrenciasEsperadas is { } ocorrencias ? EscritaCanonica.TextosEmOrdemCanonica(ocorrencias) : null;

    private static JsonArray SerializarBasesLegaisDaRegra(IEnumerable<BaseLegalDaComprovacao> basesLegais) =>
        EscritaCanonica.OrdenarPorConteudo(basesLegais.Select(static b => new JsonObject
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            ["abrangencia"] = b.Abrangencia.ToCodigo(),
            ["situacao"] = b.Situacao.ToCodigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        }));

    private static JsonArray SerializarExigencias(IReadOnlyCollection<DocumentoExigido> exigencias)
    {
        IOrderedEnumerable<DocumentoExigido> ordenadas = exigencias
            .OrderBy(static e => e.ExigidoNaFaseId)
            .ThenBy(static e => e.TipoDocumentoOrigemId)
            .ThenBy(
                static e => EscritaCanonica.PerfilAtual.Serializar(SerializarExigenciaSemIdentidade(e)),
                ComparadorLexicograficoDeBytes.Instancia)
            .ThenBy(static e => e.Id);

        return new JsonArray([.. ordenadas.Select(static e =>
        {
            JsonObject item = SerializarExigenciaSemIdentidade(e);
            item.Insert(0, "exigenciaId", JsonValue.Create(e.Id));
            return (JsonNode)item;
        })]);
    }

    private static JsonObject SerializarExigenciaSemIdentidade(DocumentoExigido exigencia)
    {
        JsonObject item = SerializarIdentificacaoDaExigencia(exigencia);
        AcrescentarCondicoesDaExigencia(item, exigencia);
        return item;
    }

    private static JsonObject SerializarIdentificacaoDaExigencia(DocumentoExigido exigencia) => new()
    {
        ["tipoDocumentoOrigemId"] = exigencia.TipoDocumentoOrigemId,
        ["tipoDocumentoCodigo"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCodigo),
        ["tipoDocumentoNome"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoNome),
        ["tipoDocumentoCategoria"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCategoria),
        ["classificacaoProtecao"] = ClassificacoesDeProtecao.ParaTokenCanonico(exigencia.ClassificacaoProtecao),
        ["finalidadeTratamento"] = HashCanonicalComputer.NormalizeNfc(exigencia.FinalidadeTratamento),
        ["hipoteseLegal"] = HashCanonicalComputer.NormalizeNfc(exigencia.HipoteseLegal),
        ["fatosComprovados"] = new JsonArray([.. exigencia.FatosComprovados.Order(StringComparer.Ordinal)
            .Select(static f => (JsonNode)JsonValue.Create(HashCanonicalComputer.NormalizeNfc(f))!)]),
    };

    private static void AcrescentarCondicoesDaExigencia(JsonObject item, DocumentoExigido exigencia)
    {
        item["exigidoNaFaseId"] = exigencia.ExigidoNaFaseId;
        item["exigidoNaEtapaId"] = exigencia.ExigidoNaEtapaId;
        item["finalidade"] = exigencia.Finalidade is { } finalidade ? EstruturaFormulario.ParaToken(finalidade) : null;
        item["aplicabilidade"] = exigencia.Aplicabilidade.ToString();
        item["obrigatorio"] = exigencia.Obrigatorio;
        item["consequenciaIndeferimento"] = exigencia.ConsequenciaIndeferimento is { } consequencia
            ? HashCanonicalComputer.NormalizeNfc(consequencia)
            : null;
        item["condicaoGatilho"] = SerializarCondicaoGatilho(exigencia.Condicoes);
        item["basesLegais"] = SerializarBasesLegais(exigencia.BasesLegaisResolvidas());
        item["idadeMaximaEmissao"] = exigencia.IdadeMaximaEmissao is { } idade ? SerializarIdadeMaximaEmissao(idade) : null;
        item["formatosPermitidos"] = SerializarFormatosPermitidos(exigencia.FormatosPermitidos);
        item["tamanhoMaximoBytes"] = exigencia.TamanhoMaximoBytes;
        item["modelo"] = exigencia.Modelo is { } modelo ? SerializarModelo(modelo) : null;
    }

    private static JsonObject SerializarModelo(ModeloDaExigencia modelo) => new()
    {
        ["modeloId"] = modelo.ModeloId,
        ["nomeArquivo"] = HashCanonicalComputer.NormalizeNfc(modelo.NomeArquivo),
        ["formato"] = ModeloDeDocumento.TokenDe(modelo.Formato),
        ["hashSha256"] = modelo.HashSha256,
    };

    private static JsonObject SerializarFormatosPermitidos(FormatosPermitidos formatosPermitidos) => new()
    {
        ["qualquer"] = formatosPermitidos.Qualquer,
        ["lista"] = formatosPermitidos.Lista is { } lista
            ? EscritaCanonica.OrdenarPorConteudo(lista.Select(static e => new JsonObject
            {
                ["formato"] = e.Formato.ToCodigo(),
                ["tamanhoMaximoBytesMax"] = e.TamanhoMaximoBytesMax,
            }))
            : null,
    };

    private static JsonArray? SerializarCondicaoGatilho(IReadOnlyCollection<CondicaoGatilho> condicoes) =>
        SerializacaoDosPredicados.SerializarDnf(condicoes.Select(static c => (c.Clausula, c.Fato, c.Operador, c.Valor)));

    private static JsonArray SerializarBasesLegais(IEnumerable<DocumentoExigidoBaseLegal> basesLegais) =>
        EscritaCanonica.OrdenarPorConteudo(basesLegais.Select(static b => new JsonObject
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            ["abrangencia"] = b.Abrangencia.ToCodigo(),
            ["situacao"] = b.Situacao.ToCodigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        }));

    private static JsonObject SerializarIdadeMaximaEmissao(IdadeMaximaEmissao idade) => new()
    {
        ["valor"] = idade.Valor,
        ["unidade"] = idade.Unidade.ToCodigo(),
        ["referenciaTipo"] = idade.ReferenciaTipo.ToCodigo(),
        ["data"] = idade.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["referenciaFaseId"] = idade.ReferenciaFaseId,
    };

    private static JsonArray SerializarObrigatoriedades(ResultadoConformidade? conformidade)
    {
        if (conformidade is null)
        {
            return [];
        }

        return EscritaCanonica.OrdenarPorConteudo(conformidade.Regras.Select(SerializarObrigatoriedade));
    }

    private static JsonObject SerializarObrigatoriedade(RegraAvaliada regra) => new()
    {
        ["regraId"] = regra.RegraId,
        ["regraCodigo"] = HashCanonicalComputer.NormalizeNfc(regra.RegraCodigo),
        ["categoria"] = regra.Categoria.ToString(),
        ["tipoProcessoCodigoAvaliado"] = HashCanonicalComputer.NormalizeNfc(regra.TipoProcessoCodigoAvaliado),
        ["predicado"] = SerializacaoDosPredicados.SerializarPredicadoObrigatoriedade(regra.Predicado),
        ["aprovada"] = regra.Aprovada,
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(regra.BaseLegal),
        ["atoNormativoUrl"] = regra.AtoNormativoUrl is { } url ? HashCanonicalComputer.NormalizeNfc(url) : null,
        ["portariaInterna"] = regra.PortariaInterna is { } portaria ? HashCanonicalComputer.NormalizeNfc(portaria) : null,
        ["descricaoHumana"] = HashCanonicalComputer.NormalizeNfc(regra.DescricaoHumana),
        ["vigenciaInicio"] = regra.VigenciaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["vigenciaFim"] = regra.VigenciaFim?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["hash"] = regra.Hash,
    };
}
