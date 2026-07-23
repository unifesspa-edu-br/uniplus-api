namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Codec da versão <c>1.3</c> do envelope (Story #919, RN08, ADR-0109 D1): acrescenta o
/// bloco <c>documentosExigidos.metadadosFatos</c> — o metadado (domínio, cardinalidade,
/// origem, binding, ponto de resolução, descrições por valor) de cada fato do candidato
/// citado em alguma condição de gatilho, congelado ao lado da condição bruta
/// <c>{fato, operador, valor}</c> que já era congelada desde a 1.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Encoder congelado (Story #923, bump para 1.4 — ADR-0109 D1):</b> até aqui, este
/// método delegava a <see cref="SnapshotPublicacaoCanonicalizer"/>, que era "o
/// canonicalizador de hoje". Com o bump para 1.4 (acréscimo do bloco
/// <c>arvoreSatisfacao</c>), <see cref="SnapshotPublicacaoCanonicalizer"/> passou a
/// emitir 1.4 — é ele quem os handlers de escrita injetam como o encoder vivo. Este
/// método é agora a ÚNICA fonte de verdade de como um envelope 1.3 é produzido: uma
/// cópia autossuficiente do que o canonicalizador emitia neste instante, para que o
/// round-trip das versões 1.3 já publicadas continue verificável para sempre, imune a
/// qualquer refactor futuro do canonicalizador vivo — exatamente o que aconteceu com
/// <see cref="EnvelopeCodecV12"/> no bump anterior (1.2 → 1.3).
/// </para>
/// <para>
/// O decoder reaproveita os métodos <c>internal</c> de <see cref="EnvelopeCodecV11"/>
/// (via <see cref="EnvelopeCodecV12"/>, que já os reaproveita) para os 11 blocos cuja
/// FORMA não mudou desde a 1.1 — <c>documentosExigidos</c> (o bloco cuja forma muda
/// nesta versão) ganha um leitor próprio aqui, que por sua vez reaproveita
/// <see cref="EnvelopeCodecV12.LerExigencias"/>, <see cref="EnvelopeCodecV12.LerObrigatoriedades"/>
/// e <see cref="EnvelopeCodecV12.LerReferenciaTemporalFatosPolitica"/> (as sub-chaves cuja
/// forma NÃO muda) e só acrescenta <c>LerMetadadosFatos</c>. O decoder 1.3 NÃO ganha
/// <c>arvoreSatisfacao</c> — a 1.3 nunca teve essa chave, e um envelope histórico "1.3"
/// não a tem nos bytes (por isso <c>GrafoConfiguracao.NosExigencia</c> é sempre <c>[]</c>
/// aqui, igual à 1.1/1.2).
/// </para>
/// </remarks>
internal static class EnvelopeCodecV13
{
    private static readonly string[] Stubs =
    [
        "formulario",
        "cascataRemanejamento",
        "divulgacao",
        "identidadesUnidade",
    ];

    private static readonly string[] BlocosReais =
    [
        "periodo",
        "etapas",
        "distribuicao",
        "modalidades",
        "ofertas",
        "atendimento",
        "bonusRegional",
        "criteriosDesempate",
        "classificacao",
        "hashesEdital",
        "cronogramaFases",
        "documentosExigidos",
        "vagas",
    ];


    /// <summary>
    /// Story #919: a única leitura de bloco que difere de <see cref="EnvelopeCodecV12"/>.
    /// <c>exigencias</c>/<c>obrigatoriedades</c>/<c>referenciaTemporalFatos</c> mantêm a
    /// MESMA forma da 1.2 (reaproveitam os leitores de <see cref="EnvelopeCodecV12"/>);
    /// <c>metadadosFatos</c> é a chave nova (RN08). <c>internal</c>: a forma de
    /// <c>documentosExigidos</c> não muda entre a 1.3 e a 1.4 (Story #923 acrescenta
    /// <c>arvoreSatisfacao</c> como bloco de topo IRMÃO, não como mudança deste bloco) —
    /// <see cref="EnvelopeCodecV14"/> reaproveita este leitor tal qual, mesma técnica de
    /// <see cref="EnvelopeCodecV12"/> para os leitores que sobrevivem a um bump.
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

        IReadOnlyList<DocumentoExigido> exigencias = EnvelopeCodecV12.LerExigencias(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        ResultadoConformidade? conformidade = EnvelopeCodecV12.LerObrigatoriedades(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        ReferenciaTemporalFatos? referenciaTemporalFatos = EnvelopeCodecV12.LerReferenciaTemporalFatosPolitica(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        // A data resolvida só é lida para participar do payload fechado (ExigirChaves
        // acima já a exige); a prova de que ela bate com a política é o round-trip
        // reidratar→recanonicalizar, não uma comparação aqui.
        leitor.DataOpcional(bloco, "dataReferenciaFatos", "documentosExigidos");
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        IReadOnlyDictionary<string, MetadadoFatoCongelado> metadadosFatos = LerMetadadosFatos(leitor, bloco);

        return leitor.Falhou
            ? (null, [], null, null)
            : (conformidade, exigencias, referenciaTemporalFatos, metadadosFatos);
    }

    /// <summary>
    /// Simétrico de <c>SnapshotPublicacaoCanonicalizer.SerializarMetadadosFatos</c>: array
    /// ordenado por <c>codigo</c> (o encoder já ordena — este leitor não reordena, só
    /// decodifica item a item), chaves fechadas por item. Código duplicado no array é
    /// envelope malformado (o encoder nunca emite duas entradas para o mesmo fato).
    /// </summary>
    private static Dictionary<string, MetadadoFatoCongelado> LerMetadadosFatos(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "metadadosFatos", "documentosExigidos");
        if (leitor.Falhou)
        {
            return new Dictionary<string, MetadadoFatoCongelado>(StringComparer.Ordinal);
        }

        Dictionary<string, MetadadoFatoCongelado> metadados = new(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"documentosExigidos.metadadosFatos[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "documentosExigidos.metadadosFatos");
            leitor.ExigirChaves(
                item, path,
                "fatoCodigo", "dominio", "origem", "cardinalidade", "pontoResolucao", "binding",
                "valoresDominio", "valoresDominioDeclarados");

            string codigo = leitor.TextoNaoVazio(item, "fatoCodigo", path);
            string dominio = leitor.TextoNaoVazio(item, "dominio", path);
            string origem = leitor.TextoNaoVazio(item, "origem", path);
            string cardinalidade = leitor.TextoNaoVazio(item, "cardinalidade", path);
            string pontoResolucao = leitor.TextoNaoVazio(item, "pontoResolucao", path);
            string binding = leitor.TextoNaoVazio(item, "binding", path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            IReadOnlyList<string>? valoresDominio = LerValoresDominio(leitor, item, path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDeclarados = LerValoresDominioDeclarados(leitor, item, path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            if (!metadados.TryAdd(codigo, new MetadadoFatoCongelado(
                codigo, dominio, origem, cardinalidade, pontoResolucao, binding, valoresDominio, valoresDeclarados)))
            {
                leitor.Propagar<MetadadoFatoCongelado>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}': o fato '{codigo}' aparece mais de uma vez em 'metadadosFatos' — cada fato tem no máximo um metadado congelado."));
                return metadados;
            }
        }

        return metadados;
    }

    /// <summary>
    /// <c>valoresDominio</c> — <see langword="null"/> significante (categórico de escopo
    /// dinâmico, booleano ou numérico), array quando o fato é categórico estático. Mesma
    /// técnica de <c>EnvelopeCodecV12.LerFormatosPermitidos</c> para o campo nulo-ou-array:
    /// leitura do <see cref="JsonNode"/> bruto, não <see cref="LeitorEnvelope.Array"/> (que
    /// rejeitaria <see langword="null"/>).
    /// </summary>
    private static IReadOnlyList<string>? LerValoresDominio(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.valoresDominio";
        if (item["valoresDominio"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray)
        {
            return leitor.Propagar<IReadOnlyList<string>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array de textos ou null."));
        }

        return leitor.Textos(item, "valoresDominio", pathPai);
    }

    /// <summary>Mesma técnica de <see cref="LerValoresDominio"/> para o campo nulo-ou-array.</summary>
    private static IReadOnlyList<ValorDominioDeclaradoCongelado>? LerValoresDominioDeclarados(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.valoresDominioDeclarados";
        if (item["valoresDominioDeclarados"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray array)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array ou null."));
        }

        List<ValorDominioDeclaradoCongelado> valores = [];
        for (int i = 0; i < array.Count; i++)
        {
            string itemPath = $"{path}[{i}]";
            JsonObject valorItem = leitor.ItemObjeto(array, i, path);
            leitor.ExigirChaves(valorItem, itemPath, "valorCodigo", "descricao");

            string codigoValor = leitor.TextoNaoVazio(valorItem, "valorCodigo", itemPath);
            string? descricao = leitor.TextoOpcional(valorItem, "descricao", itemPath);
            if (leitor.Falhou)
            {
                return null;
            }

            valores.Add(new ValorDominioDeclaradoCongelado(codigoValor, descricao));
        }

        return valores;
    }
}
