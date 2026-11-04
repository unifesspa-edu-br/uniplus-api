namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura dos metadados dos fatos congelados na publicação: domínio, origem, cardinalidade e valores declarados.
/// </summary>
internal static class LeituraDosMetadadosDosFatos
{
    /// <summary>
    /// Simétrico de <see cref="SerializacaoDosFatos.SerializarMetadadosFatos"/>: array
    /// ordenado por <c>codigo</c> (o encoder já ordena — este leitor não reordena, só
    /// decodifica item a item), chaves fechadas por item. Código duplicado no array é
    /// envelope malformado (o encoder nunca emite duas entradas para o mesmo fato). Dentro de
    /// cada item, <c>valoresDominioDeclarados[]</c> (quando presente) recebe a mesma disciplina:
    /// <c>ordem</c> obrigatória e não negativa, <c>valorCodigo</c> sem repetição
    /// (<see cref="LeituraDosValoresDeDominio.LerValoresDominioDeclarados"/>).
    /// </summary>
    internal static Dictionary<string, MetadadoFatoCongelado> LerMetadadosFatos(LeitorEnvelope leitor, JsonObject bloco)
    {
        Dictionary<string, MetadadoFatoCongelado> metadados = new(StringComparer.Ordinal);
        JsonArray array = leitor.Array(bloco, "metadadosFatos", "documentosExigidos");
        if (leitor.Falhou)
        {
            return metadados;
        }

        for (int i = 0; i < array.Count; i++)
        {
            MetadadoFatoCongelado? metadado = LerMetadadoDoFato(
                leitor,
                leitor.ItemObjeto(array, i, "documentosExigidos.metadadosFatos"),
                $"documentosExigidos.metadadosFatos[{i}]");
            if (metadado is null)
            {
                return metadados;
            }

            if (!metadados.TryAdd(metadado.Codigo, metadado))
            {
                leitor.Recusar<object>(
                    $"'documentosExigidos.metadadosFatos[{i}]': o fato '{metadado.Codigo}' aparece mais de uma vez em 'metadadosFatos' — cada fato tem no máximo um metadado congelado.");
                return metadados;
            }
        }

        return metadados;
    }

    private static MetadadoFatoCongelado? LerMetadadoDoFato(LeitorEnvelope leitor, JsonObject item, string path)
    {
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
            return null;
        }

        IReadOnlyList<string>? valoresDominio = LerValoresDominio(leitor, item, path);
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDeclarados = leitor.Falhou
            ? null
            : LeituraDosValoresDeDominio.LerValoresDominioDeclarados(leitor, item, path);

        return leitor.Falhou
            ? null
            : new MetadadoFatoCongelado(codigo, dominio, origem, cardinalidade, pontoResolucao, binding, valoresDominio, valoresDeclarados);
    }

    /// <summary>
    /// <c>valoresDominio</c> — <see langword="null"/> significante (categórico de escopo
    /// dinâmico, booleano ou numérico), array quando o fato é categórico estático. Mesma
    /// técnica de <c>LeituraDosFormatosEBasesLegais.LerFormatosPermitidos</c> para o campo nulo-ou-array:
    /// leitura do <see cref="JsonNode"/> bruto, não <see cref="LeitorEnvelope.Array"/> (que
    /// rejeitaria <see langword="null"/>).
    /// </summary>
    internal static IReadOnlyList<string>? LerValoresDominio(LeitorEnvelope leitor, JsonObject item, string pathPai)
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
}
