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
            ["referenciaTemporalDosDados"] = processo.ReferenciaTemporalFatos is { } referencia ? SerializarReferencia(referencia) : null,
            ["dataDeReferenciaDosDados"] = Domain.ProcessosSeletivos.Fatos.ReferenciaTemporalDosFatos.ResolverDataReferenciaFatos(processo.Declaracao(), fusoHorario) is { } data
            ? data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null,
            ["definicoesDosDados"] = SerializacaoDosFatos.SerializarMetadadosFatos(metadadosFatosCongelados),
        };

    internal static JsonArray SerializarRegrasDeComprovacao(ProcessoSeletivo processo) =>
        new([.. processo.RegrasDeComprovacaoSemGrupo.OrderBy(static r => r.Ordem).Select(static r => (JsonNode)SerializarRegra(r))]);

    private static JsonObject SerializarRegra(RegraDeComprovacao regra)
    {
        JsonObject objeto = DadosDaRegra(regra);
        ContextoDosRotulos.Acrescentar(objeto, "forma", (FormaDeComprovacao?)regra.Forma);
        ContextoDosRotulos.Acrescentar(objeto, "consequenciaDoDescumprimento", regra.ConsequenciaDoDescumprimento);
        ContextoDosRotulos.Acrescentar(objeto, "criterioDeDistincao", regra.Distincao?.Criterio);
        return objeto;
    }

    private static JsonObject DadosDaRegra(RegraDeComprovacao regra)
    {
        JsonObject objeto = IdentificacaoDaRegra(regra);
        objeto["dataReferencia"] = regra.Distincao?.DataReferencia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        objeto["ocorrenciasEsperadas"] = SerializarOcorrencias(regra.Distincao);
        objeto["grupoDoFormularioParaRepeticao"] = regra.GrupoDoFormularioParaRepeticao;
        objeto["itens"] = new JsonArray([.. regra.Itens.OrderBy(static f => f.Ordem).Select(static f => (JsonNode)SerializarRegra(f))]);
        return objeto;
    }

    private static JsonObject IdentificacaoDaRegra(RegraDeComprovacao regra) => new()
    {
        ["id"] = regra.Id,
        ["ordem"] = regra.Ordem,
        ["forma"] = regra.Forma.Codigo(),
        ["exigenciaId"] = regra.Forma == FormaDeComprovacao.Documento ? regra.DocumentoExigidoId : null,
        ["quantidadeMinima"] = regra.QuantidadeMinima,
        ["abrePrazoParaReenvio"] = regra.AbrePrazoParaReenvio,
        ["consequenciaDoDescumprimento"] = regra.ConsequenciaDoDescumprimento?.Codigo(),
        ["basesLegais"] = SerializarBasesLegaisDaRegra(regra.BasesLegaisResolvidas()),
        ["criterioDeDistincao"] = regra.Distincao?.Criterio?.Codigo(),
    };

    private static JsonArray? SerializarOcorrencias(DistincaoDasApresentacoes? distincao) =>
        distincao?.OcorrenciasEsperadas is { } ocorrencias ? EscritaCanonica.TextosEmOrdemCanonica(ocorrencias) : null;

    private static JsonArray SerializarBasesLegaisDaRegra(IEnumerable<BaseLegalDaComprovacao> basesLegais) =>
        EscritaCanonica.OrdenarPorConteudo(basesLegais.Select(static b => SerializarBaseLegalDaRegra(b)));

    private static JsonObject SerializarBaseLegalDaRegra(BaseLegalDaComprovacao b)
    {
        JsonObject objeto = new()
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            ["abrangencia"] = b.Abrangencia.Codigo(),
            ["situacao"] = b.Situacao.Codigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        };
        ContextoDosRotulos.Acrescentar(objeto, "abrangencia", (TipoAbrangencia?)b.Abrangencia);
        ContextoDosRotulos.Acrescentar(objeto, "situacao", (StatusBaseLegal?)b.Situacao);
        return objeto;
    }

    internal static JsonArray SerializarExigencias(IReadOnlyCollection<DocumentoExigido> exigencias)
    {
        IOrderedEnumerable<DocumentoExigido> ordenadas = exigencias
            .OrderBy(static e => e.ExigidoNaFaseId)
            .ThenBy(static e => e.TipoDocumentoOrigemId)
            .ThenBy(
                static e => EscritaCanonica.PerfilAtual.Serializar(SerializarExigenciaSemIdentidade(e)),
                ComparadorLexicograficoDeBytes.Instancia)
            .ThenBy(static e => e.Id);

        return new JsonArray([.. ordenadas.Select(SerializarExigencia)]);
    }

    private static JsonNode SerializarExigencia(DocumentoExigido exigencia)
    {
        JsonObject item = SerializarExigenciaSemIdentidade(exigencia);
        item.Insert(0, "exigenciaId", JsonValue.Create(exigencia.Id));
        return item;
    }

    private static JsonObject SerializarExigenciaSemIdentidade(DocumentoExigido exigencia)
    {
        JsonObject item = SerializarIdentificacaoDaExigencia(exigencia);
        AcrescentarCondicoesDaExigencia(item, exigencia);
        AcrescentarRequisitosDaExigencia(item, exigencia);
        return item;
    }

    private static JsonObject SerializarIdentificacaoDaExigencia(DocumentoExigido exigencia) => new()
    {
        ["chave"] = exigencia.Chave.Valor,
        ["tipoDocumentoOrigemId"] = exigencia.TipoDocumentoOrigemId,
        ["tipoDocumentoCodigo"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCodigo),
        ["tipoDocumentoNome"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoNome),
        ["tipoDocumentoCategoria"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCategoria),
        ["classificacaoProtecao"] = ClassificacoesDeProtecao.ParaTokenCanonico(exigencia.ClassificacaoProtecao),
        ["finalidadeTratamento"] = HashCanonicalComputer.NormalizeNfc(exigencia.FinalidadeTratamento),
        ["hipoteseLegal"] = HashCanonicalComputer.NormalizeNfc(exigencia.HipoteseLegal),
        ["dadosComprovados"] = new JsonArray([.. exigencia.FatosComprovados.Order(StringComparer.Ordinal)
            .Select(static f => (JsonNode)JsonValue.Create(HashCanonicalComputer.NormalizeNfc(f))!)]),
    };

    private static void AcrescentarCondicoesDaExigencia(JsonObject item, DocumentoExigido exigencia)
    {
        item["exigidoNaFaseId"] = exigencia.ExigidoNaFaseId;
        item["exigidoNaEtapaId"] = exigencia.ExigidoNaEtapaId;
        item["finalidade"] = exigencia.Finalidade?.Codigo();
        item["aplicabilidade"] = exigencia.Aplicabilidade.Codigo();
        ContextoDosRotulos.Acrescentar(item, "finalidade", exigencia.Finalidade);
        ContextoDosRotulos.Acrescentar(item, "aplicabilidade", (Aplicabilidade?)exigencia.Aplicabilidade);
        AcrescentarDetalhesDaExigencia(item, exigencia);
    }

    private static void AcrescentarDetalhesDaExigencia(JsonObject item, DocumentoExigido exigencia)
    {
        item["obrigatorio"] = exigencia.Obrigatorio;
        item["abrePrazoParaReenvio"] = exigencia.AbrePrazoParaReenvio;
        item["consequenciaDoDescumprimento"] = exigencia.ConsequenciaDoDescumprimento?.Codigo();
        ContextoDosRotulos.Acrescentar(item, "consequenciaDoDescumprimento", exigencia.ConsequenciaDoDescumprimento);
        item["condicoes"] = SerializarCondicaoGatilho(exigencia.Condicoes);
        item["basesLegais"] = SerializarBasesLegais(exigencia.BasesLegaisResolvidas());
    }

    private static void AcrescentarRequisitosDaExigencia(JsonObject item, DocumentoExigido exigencia)
    {
        item["prazoDeEmissao"] = SerializacaoDoPrazoEDaValidade.Prazo(exigencia.PrazoDeEmissao);
        item["validade"] = SerializacaoDoPrazoEDaValidade.Validade(exigencia.Validade);
        item["formatosPermitidos"] = SerializarFormatosPermitidos(exigencia.FormatosPermitidos);
        item["tamanhoMaximoBytes"] = exigencia.TamanhoMaximoBytes;
        item["modelo"] = exigencia.Modelo is { } modelo ? SerializarModelo(modelo) : null;
    }

    private static JsonObject SerializarModelo(ModeloDaExigencia modelo)
    {
        JsonObject objeto = DadosDoModelo(modelo);
        ContextoDosRotulos.Acrescentar(objeto, "formato", (FormatoDeDocumento?)modelo.Formato);
        return objeto;
    }

    private static JsonObject DadosDoModelo(ModeloDaExigencia modelo) => new()
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
            ? EscritaCanonica.OrdenarPorConteudo(lista.Select(SerializarFormatoPermitido))
            : null,
    };

    private static JsonArray? SerializarCondicaoGatilho(IReadOnlyCollection<CondicaoGatilho> condicoes) =>
        SerializacaoDosPredicados.SerializarDnf(condicoes.Select(static c => (c.Clausula, c.Fato, c.Operador, c.Valor)));

    private static JsonArray SerializarBasesLegais(IEnumerable<DocumentoExigidoBaseLegal> basesLegais) =>
        EscritaCanonica.OrdenarPorConteudo(basesLegais.Select(SerializarBaseLegal));

    private static JsonObject SerializarBaseLegal(DocumentoExigidoBaseLegal b)
    {
        JsonObject objeto = new()
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            ["abrangencia"] = b.Abrangencia.Codigo(),
            ["situacao"] = b.Situacao.Codigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        };
        ContextoDosRotulos.Acrescentar(objeto, "abrangencia", (TipoAbrangencia?)b.Abrangencia);
        ContextoDosRotulos.Acrescentar(objeto, "situacao", (StatusBaseLegal?)b.Situacao);
        return objeto;
    }

    private static JsonArray SerializarObrigatoriedades(ResultadoConformidade? conformidade)
    {
        if (conformidade is null)
        {
            return [];
        }

        return EscritaCanonica.OrdenarPorConteudo(conformidade.Regras.Select(SerializarObrigatoriedade));
    }

    private static JsonObject SerializarObrigatoriedade(RegraAvaliada regra)
    {
        JsonObject objeto = DadosDaObrigatoriedade(regra);
        ContextoDosRotulos.Acrescentar(objeto, "categoria", (CategoriaObrigatoriedade?)regra.Categoria);
        return objeto;
    }

    private static JsonObject DadosDaObrigatoriedade(RegraAvaliada regra)
    {
        JsonObject objeto = IdentificacaoDaObrigatoriedade(regra);
        objeto["portariaInterna"] = regra.PortariaInterna is { } portaria ? HashCanonicalComputer.NormalizeNfc(portaria) : null;
        objeto["descricaoHumana"] = HashCanonicalComputer.NormalizeNfc(regra.DescricaoHumana);
        objeto["vigenciaInicio"] = regra.VigenciaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        objeto["vigenciaFim"] = regra.VigenciaFim?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        objeto["hash"] = regra.Hash;
        return objeto;
    }

    private static JsonObject IdentificacaoDaObrigatoriedade(RegraAvaliada regra) => new()
    {
        ["regraId"] = regra.RegraId,
        ["regraCodigo"] = HashCanonicalComputer.NormalizeNfc(regra.RegraCodigo),
        ["categoria"] = regra.Categoria.Codigo(),
        ["tipoProcessoCodigoAvaliado"] = HashCanonicalComputer.NormalizeNfc(regra.TipoProcessoCodigoAvaliado),
        ["condicoes"] = SerializacaoDosPredicados.SerializarPredicadoObrigatoriedade(regra.Predicado),
        ["aprovada"] = regra.Aprovada,
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(regra.BaseLegal),
        ["atoNormativoUrl"] = regra.AtoNormativoUrl is { } url ? HashCanonicalComputer.NormalizeNfc(url) : null,
    };

    private static JsonObject SerializarReferencia(ReferenciaTemporalFatos referencia)
    {
        JsonObject objeto = new() { ["tipo"] = referencia.Tipo.Codigo(), ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["faseId"] = referencia.FaseId };
        ContextoDosRotulos.Acrescentar(objeto, "tipo", (ReferenciaTipo?)referencia.Tipo);
        return objeto;
    }

    private static JsonObject SerializarFormatoPermitido(FormatoPermitidoEntry formato)
    {
        JsonObject objeto = new() { ["formato"] = formato.Formato.Codigo(), ["tamanhoMaximoBytesMax"] = formato.TamanhoMaximoBytesMax };
        ContextoDosRotulos.Acrescentar(objeto, "formato", (FormatoPermitido?)formato.Formato);
        return objeto;
    }
}
