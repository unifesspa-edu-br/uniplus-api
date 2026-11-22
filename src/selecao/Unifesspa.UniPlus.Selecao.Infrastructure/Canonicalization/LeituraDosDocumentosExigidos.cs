namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura dos documentos exigidos: exigências, modelo, idade máxima de emissão e obrigatoriedades.
/// </summary>
internal static class LeituraDosDocumentosExigidos
{
    /// <summary>
    /// Orquestra o bloco inteiro: <c>exigencias</c>, <c>obrigatoriedades</c> (e o predicado que as condiciona),
    /// <c>referenciaTemporalFatos</c> e <c>metadadosFatos</c>. O bloco de topo <c>regrasDeComprovacao</c> é irmão
    /// deste, e por isso não é lido aqui.
    /// </summary>
    internal static (
        ResultadoConformidade? Conformidade,
        IReadOnlyList<DocumentoExigido> DocumentosExigidos,
        ReferenciaTemporalFatos? ReferenciaTemporalFatos,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? MetadadosFatosCongelados)
        LerDocumentosExigidos(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "documentosExigidos", "$");
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        leitor.ExigirChaves(
            bloco, "documentosExigidos",
            "exigencias", "obrigatoriedades", "referenciaTemporalFatos", "dataReferenciaFatos", "metadadosFatos");

        IReadOnlyList<DocumentoExigido> exigencias = LeituraDasExigencias.LerExigencias(leitor, bloco);
        ResultadoConformidade? conformidade = LerObrigatoriedades(leitor, bloco);
        ReferenciaTemporalFatos? referenciaTemporalFatos = LeituraDosFatos.LerReferenciaTemporalFatosPolitica(leitor, bloco);

        // A data resolvida só é lida para participar do payload fechado; a prova de que ela bate com
        // a política é o round-trip reidratar e recanonicalizar, não uma comparação aqui.
        leitor.DataOpcional(bloco, "dataReferenciaFatos", "documentosExigidos");
        IReadOnlyDictionary<string, MetadadoFatoCongelado> metadadosFatos = LeituraDosMetadadosDosFatos.LerMetadadosFatos(leitor, bloco);

        return leitor.Falhou
            ? (null, [], null, null)
            : (conformidade, exigencias, referenciaTemporalFatos, metadadosFatos);
    }

    /// <summary>Reaproveita <see cref="LerPredicadoObrigatoriedade"/> para o predicado de cada regra.</summary>
    internal static ResultadoConformidade? LerObrigatoriedades(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "obrigatoriedades", "documentosExigidos");
        if (leitor.Falhou)
        {
            return null;
        }

        List<RegraAvaliada> regras = [];
        for (int i = 0; i < array.Count; i++)
        {
            RegraAvaliada? regra = LerRegraAvaliada(
                leitor,
                leitor.ItemObjeto(array, i, "documentosExigidos.obrigatoriedades"),
                $"documentosExigidos.obrigatoriedades[{i}]");
            if (regra is null)
            {
                return null;
            }

            regras.Add(regra);
        }

        return regras.Count == 0 ? null : new ResultadoConformidade(regras, []);
    }

    private static PredicadoObrigatoriedade? LerPredicadoDaRegra(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonObject predicadoJson = leitor.Objeto(item, "predicado", path);
        return leitor.Falhou ? null : LerPredicadoObrigatoriedade(leitor, predicadoJson, $"{path}.predicado");
    }

    private static RegraAvaliada? LerRegraAvaliada(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(
            item, path,
            "regraId", "regraCodigo", "categoria", "tipoProcessoCodigoAvaliado", "predicado",
            "aprovada", "baseLegal", "atoNormativoUrl", "portariaInterna", "descricaoHumana",
            "vigenciaInicio", "vigenciaFim", "hash");

        Guid regraId = leitor.Identificador(item, "regraId", path);
        string regraCodigo = leitor.TextoNaoVazio(item, "regraCodigo", path);
        CategoriaObrigatoriedade categoria = leitor.Enumeracao<CategoriaObrigatoriedade>(item, "categoria", path);
        string tipoProcessoCodigoAvaliado = leitor.TextoNaoVazio(item, "tipoProcessoCodigoAvaliado", path);
        PredicadoObrigatoriedade? predicado = LerPredicadoDaRegra(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        bool aprovada = leitor.Booleano(item, "aprovada", path);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path);
        string? atoNormativoUrl = leitor.TextoOpcional(item, "atoNormativoUrl", path);
        string? portariaInterna = leitor.TextoOpcional(item, "portariaInterna", path);
        string descricaoHumana = leitor.TextoNaoVazio(item, "descricaoHumana", path);
        DateOnly vigenciaInicio = leitor.Data(item, "vigenciaInicio", path);
        DateOnly? vigenciaFim = leitor.DataOpcional(item, "vigenciaFim", path);
        string hash = leitor.TextoNaoVazio(item, "hash", path);

        return leitor.Falhou
            ? null
            : new RegraAvaliada(
                regraId, regraCodigo, categoria, tipoProcessoCodigoAvaliado, predicado!, aprovada, null,
                baseLegal, atoNormativoUrl, portariaInterna, descricaoHumana, vigenciaInicio, vigenciaFim, hash);
    }

    /// <summary>
    /// A variante vem do campo <c>tipo</c> (o próprio envelope carrega o discriminador
    /// aqui — ao contrário de <see cref="LeituraDoDesempate.LerParametrosDesempate"/>, não há um "código de regra"
    /// externo aos parâmetros para decidir a forma; <c>PredicadoObrigatoriedade</c> já é a
    /// discriminated union completa que o domínio serializa).
    /// </summary>
    internal static PredicadoObrigatoriedade? LerPredicadoObrigatoriedade(LeitorEnvelope leitor, JsonObject predicado, string path)
    {
        leitor.ExigirChaves(predicado, path, "tipo", "parametros");
        string tipo = leitor.TextoNaoVazio(predicado, "tipo", path);
        JsonObject parametros = leitor.Objeto(predicado, "parametros", path);
        if (leitor.Falhou)
        {
            return null;
        }

        string parametrosPath = $"{path}.parametros";
        return tipo switch
        {
            "etapaObrigatoria" => LerParametroUnico(leitor, parametros, parametrosPath, "tipoEtapaCodigo", static codigo => new EtapaObrigatoria(codigo)),
            "desempateDeveIncluir" => LerParametroUnico(leitor, parametros, parametrosPath, "criterio", static criterio => new DesempateDeveIncluir(criterio)),
            "modalidadesMinimas" => LerListaDeParametro(leitor, parametros, parametrosPath, "codigos", static codigos => new ModalidadesMinimas(codigos)),
            "atendimentoDisponivel" => LerListaDeParametro(leitor, parametros, parametrosPath, "necessidades", static necessidades => new AtendimentoDisponivel(necessidades)),
            "documentoObrigatorioParaModalidade" => LerDocumentoObrigatorioParaModalidade(leitor, parametros, parametrosPath),
            "concorrenciaDuplaObrigatoria" => LerSemParametros(leitor, parametros, parametrosPath),
            "customizado" => LerCustomizado(leitor, parametros, parametrosPath),
            _ => leitor.Propagar<PredicadoObrigatoriedade>(new DomainError(
                ErrosCodecEnvelope.RegraDesconhecida,
                $"Não há variante de {nameof(PredicadoObrigatoriedade)} conhecida para o tipo '{tipo}' em '{path}'.")),
        };
    }

    private static PredicadoObrigatoriedade? LerParametroUnico(
        LeitorEnvelope leitor,
        JsonObject parametros,
        string parametrosPath,
        string chave,
        Func<string, PredicadoObrigatoriedade> criar)
    {
        leitor.ExigirChaves(parametros, parametrosPath, chave);
        string valor = leitor.TextoNaoVazio(parametros, chave, parametrosPath);
        return leitor.Falhou ? null : criar(valor);
    }

    private static PredicadoObrigatoriedade? LerListaDeParametro(
        LeitorEnvelope leitor,
        JsonObject parametros,
        string parametrosPath,
        string chave,
        Func<IReadOnlyList<string>, PredicadoObrigatoriedade> criar)
    {
        leitor.ExigirChaves(parametros, parametrosPath, chave);
        IReadOnlyList<string> valores = leitor.Textos(parametros, chave, parametrosPath);
        return leitor.Falhou ? null : criar(valores);
    }

    private static DocumentoObrigatorioParaModalidade? LerDocumentoObrigatorioParaModalidade(LeitorEnvelope leitor, JsonObject parametros, string parametrosPath)
    {
        leitor.ExigirChaves(parametros, parametrosPath, "modalidade", "tipoDocumento");
        string modalidade = leitor.TextoNaoVazio(parametros, "modalidade", parametrosPath);
        string tipoDocumento = leitor.TextoNaoVazio(parametros, "tipoDocumento", parametrosPath);
        return leitor.Falhou ? null : new DocumentoObrigatorioParaModalidade(modalidade, tipoDocumento);
    }

    private static ConcorrenciaDuplaObrigatoria? LerSemParametros(LeitorEnvelope leitor, JsonObject parametros, string parametrosPath)
    {
        leitor.ExigirChaves(parametros, parametrosPath);
        return leitor.Falhou ? null : new ConcorrenciaDuplaObrigatoria();
    }

    private static Customizado? LerCustomizado(LeitorEnvelope leitor, JsonObject parametros, string parametrosPath)
    {
        leitor.ExigirChaves(parametros, parametrosPath, "parametros");
        System.Text.Json.JsonElement parametrosDoPredicado = leitor.Valor(parametros, "parametros", parametrosPath);
        return leitor.Falhou ? null : new Customizado(parametrosDoPredicado);
    }
}
