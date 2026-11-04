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
    ///: a orquestração do bloco inteiro — <c>exigencias</c>, <c>obrigatoriedades</c>
    /// (e o predicado que as condiciona), <c>referenciaTemporalFatos</c> e a chave
    /// <c>metadadosFatos</c> (congelamento dos parâmetros na publicação, UNI-REQ-0019). O bloco de topo <c>arvoreSatisfacao</c> é
    /// IRMÃO deste, não mudança dele — por isso não aparece aqui.
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
    /// aqui — ao contrário de <see cref="LeituraDoDesempate.LerArgsDesempate"/>, não há um "código de regra"
    /// externo ao args para decidir a forma; <c>PredicadoObrigatoriedade</c> já é a
    /// discriminated union completa que o domínio serializa).
    /// </summary>
    internal static PredicadoObrigatoriedade? LerPredicadoObrigatoriedade(LeitorEnvelope leitor, JsonObject predicado, string path)
    {
        leitor.ExigirChaves(predicado, path, "tipo", "args");
        string tipo = leitor.TextoNaoVazio(predicado, "tipo", path);
        JsonObject args = leitor.Objeto(predicado, "args", path);
        if (leitor.Falhou)
        {
            return null;
        }

        string argsPath = $"{path}.args";
        return tipo switch
        {
            "etapaObrigatoria" => LerArgumentoUnico(leitor, args, argsPath, "tipoEtapaCodigo", static codigo => new EtapaObrigatoria(codigo)),
            "desempateDeveIncluir" => LerArgumentoUnico(leitor, args, argsPath, "criterio", static criterio => new DesempateDeveIncluir(criterio)),
            "modalidadesMinimas" => LerListaDeArgumento(leitor, args, argsPath, "codigos", static codigos => new ModalidadesMinimas(codigos)),
            "atendimentoDisponivel" => LerListaDeArgumento(leitor, args, argsPath, "necessidades", static necessidades => new AtendimentoDisponivel(necessidades)),
            "documentoObrigatorioParaModalidade" => LerDocumentoObrigatorioParaModalidade(leitor, args, argsPath),
            "concorrenciaDuplaObrigatoria" => LerSemArgumentos(leitor, args, argsPath),
            "customizado" => LerCustomizado(leitor, args, argsPath),
            _ => leitor.Propagar<PredicadoObrigatoriedade>(new DomainError(
                ErrosCodecEnvelope.RegraDesconhecida,
                $"Não há variante de {nameof(PredicadoObrigatoriedade)} conhecida para o tipo '{tipo}' em '{path}'.")),
        };
    }

    private static PredicadoObrigatoriedade? LerArgumentoUnico(
        LeitorEnvelope leitor,
        JsonObject args,
        string argsPath,
        string chave,
        Func<string, PredicadoObrigatoriedade> criar)
    {
        leitor.ExigirChaves(args, argsPath, chave);
        string valor = leitor.TextoNaoVazio(args, chave, argsPath);
        return leitor.Falhou ? null : criar(valor);
    }

    private static PredicadoObrigatoriedade? LerListaDeArgumento(
        LeitorEnvelope leitor,
        JsonObject args,
        string argsPath,
        string chave,
        Func<IReadOnlyList<string>, PredicadoObrigatoriedade> criar)
    {
        leitor.ExigirChaves(args, argsPath, chave);
        IReadOnlyList<string> valores = leitor.Textos(args, chave, argsPath);
        return leitor.Falhou ? null : criar(valores);
    }

    private static DocumentoObrigatorioParaModalidade? LerDocumentoObrigatorioParaModalidade(LeitorEnvelope leitor, JsonObject args, string argsPath)
    {
        leitor.ExigirChaves(args, argsPath, "modalidade", "tipoDocumento");
        string modalidade = leitor.TextoNaoVazio(args, "modalidade", argsPath);
        string tipoDocumento = leitor.TextoNaoVazio(args, "tipoDocumento", argsPath);
        return leitor.Falhou ? null : new DocumentoObrigatorioParaModalidade(modalidade, tipoDocumento);
    }

    private static ConcorrenciaDuplaObrigatoria? LerSemArgumentos(LeitorEnvelope leitor, JsonObject args, string argsPath)
    {
        leitor.ExigirChaves(args, argsPath);
        return leitor.Falhou ? null : new ConcorrenciaDuplaObrigatoria();
    }

    private static Customizado? LerCustomizado(LeitorEnvelope leitor, JsonObject args, string argsPath)
    {
        leitor.ExigirChaves(args, argsPath, "parametros");
        System.Text.Json.JsonElement parametros = leitor.Valor(args, "parametros", argsPath);
        return leitor.Falhou ? null : new Customizado(parametros);
    }
}
