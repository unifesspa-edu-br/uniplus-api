namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura de enumerações, listas de texto, valores livres e referências de regra do envelope.
/// </summary>
internal static class LeituraDeEnumsEListas
{
    /// <summary>
    /// Enum pelo <b>nome exato</b> — o encoder escreve <c>ToString()</c>. Aceitar
    /// case-insensitive faria dois envelopes textualmente distintos reidratarem no mesmo
    /// valor, e o round-trip acusaria uma divergência que a leitura já deveria ter
    /// recusado.
    /// </summary>
    public static TEnum Enumeracao<TEnum>(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
        where TEnum : struct, Enum
    {
        if (leitor.Falhou)
        {
            return default;
        }

        string texto = leitor.Texto(pai, chave, path);
        if (leitor.Falhou)
        {
            return default;
        }

        if (!Enum.TryParse(texto, ignoreCase: false, out TEnum valor) || !Enum.IsDefined(valor))
        {
            return leitor.Malformado<TEnum>($"{path}.{chave}", $"valor '{texto}' não é um {typeof(TEnum).Name} conhecido.");
        }

        return valor;
    }

    /// <summary>
    /// Variante opcional de <see cref="Enumeracao{TEnum}"/> — usada pelos pares de
    /// suspensividade de <c>ArgsRegraPrazoRecurso</c>, em que
    /// <see langword="null"/> é valor legítimo ("esta instância não bloqueia").
    /// </summary>
    public static TEnum? EnumeracaoOpcional<TEnum>(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
        where TEnum : struct, Enum
    {
        if (leitor.Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        return node is null ? null : leitor.Enumeracao<TEnum>(pai, chave, path);
    }

    /// <summary>
    /// Array de escalares de texto — cada item <b>não-branco</b> e <b>já normalizado</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Um item em branco num conjunto de códigos (<c>ocorrenciasEsperadas</c>,
    /// <c>valoresDominio</c>) é um <b>código que não diz nada</b>; um item com espaços nas
    /// pontas (<c>" PCD "</c>) é pior: ele é um código que <b>ninguém reconhece</b>. Os dois
    /// entram no jsonb, fazem round-trip perfeito (o encoder reemite o array item a item,
    /// verbatim) e viram valores que o motor teria de avaliar por <b>comparação exata</b>. O
    /// cadastro nunca os produz, porque normaliza o que grava.
    /// </para>
    /// <para>
    /// <b>A guarda recusa; ela não normaliza.</b> Aparar o espaço aqui mudaria o valor em
    /// relação aos bytes congelados, a recanonicalização produziria bytes distintos, e a
    /// prova de round-trip <b>recusaria o descarte de um certame legítimo</b> — trocando um
    /// dado sujo por um certame sem descarte. O envelope ou está na forma que o cadastro
    /// produz, ou não é reidratável.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Textos(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return [];
        }

        JsonArray array = leitor.Array(pai, chave, path);
        if (leitor.Falhou)
        {
            return [];
        }

        List<string> valores = [];
        for (int i = 0; i < array.Count; i++)
        {
            if (DefeitoDoItemDeTexto(array[i]) is { } defeito)
            {
                return leitor.Malformado<IReadOnlyList<string>>($"{path}.{chave}[{i}]", defeito) ?? [];
            }

            valores.Add(array[i]!.GetValue<string>());
        }

        return valores;
    }

    private static string? DefeitoDoItemDeTexto(JsonNode? item)
    {
        if (item is null || item.GetValueKind() != System.Text.Json.JsonValueKind.String)
        {
            return "esperado um texto.";
        }

        string valor = item.GetValue<string>();
        if (string.IsNullOrWhiteSpace(valor))
        {
            return "esperado um texto não vazio.";
        }

        return string.Equals(valor, valor.Trim(), StringComparison.Ordinal)
            ? null
            : $"esperado um texto já normalizado (sem espaços nas pontas), encontrado '{valor}'.";
    }

    /// <summary>
    /// Valor JSON bruto — escalar ou array, na forma exata em que foi escrito —
    /// usado pelos campos que o Domain trata como <see cref="System.Text.Json.JsonElement"/>
    /// (<c>CondicaoDnf.Valor</c>, ADR-0111). Sem interpretação estrutural nesta
    /// camada: a forma é conferida pela factory do domínio (<c>CondicaoDnf.Criar</c>).
    /// </summary>
    public static System.Text.Json.JsonElement Valor(this LeitorEnvelope leitor, JsonObject pai, string chave, string path)
    {
        if (leitor.Falhou)
        {
            return default;
        }

        JsonNode? node = pai[chave];
        if (node is null)
        {
            return leitor.Malformado<System.Text.Json.JsonElement>($"{path}.{chave}", "esperado um valor, encontrado null.");
        }

        return node.Deserialize<System.Text.Json.JsonElement>();
    }

    /// <summary>
    /// A tripla <c>{codigo, versao, hash}</c> de uma referência ao <c>rol_de_regras</c>,
    /// com o <paramref name="rol"/> <b>fechado</b>: o vocabulário não é validado pelas
    /// factories do domínio — um código desconhecido cairia no ramo default delas
    /// (institucional, cálculo local) e reconstruiria configuração diferente da
    /// congelada, com round-trip perfeito.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>O que esta leitura deliberadamente NÃO faz: resolver a tripla contra o catálogo
    /// vivo.</b> O caminho de comando resolve — ele lê <c>(codigo, versao)</c> no
    /// <c>rol_de_regras</c> e copia de lá o <c>hash</c> real. A reidratação <b>não pode</b>
    /// fazer o mesmo, e a razão é o próprio congelamento: a referência é <b>snapshot-copy
    /// por valor</b> (ADR-0061), e a regra que ela cita <b>pode ter sido retirada ou
    /// versionada</b> depois da publicação. Exigir que ela ainda exista tornaria
    /// <b>irreidratável</b> um certame legitimamente publicado — o descarte da sessão
    /// editorial dele passaria a falhar, e a configuração congelada, que é a evidência
    /// jurídica, ficaria inalcançável. O passado não se reinterpreta contra o catálogo de
    /// hoje (congelamento dos parâmetros na publicação, UNI-REQ-0019).
    /// </para>
    /// <para>
    /// O que resta verificável <b>sem</b> estado vivo é o que se verifica: o código pertence
    /// ao rol da forma <c>1.1</c>, a versão e o código não são brancos e cabem nas colunas, e
    /// o hash tem o shape content-addressable (<c>ReferenciaRegra.Criar</c>). A prova de que
    /// a tripla <i>existiu</i> é o hash do <b>envelope</b>, que cobre os três campos.
    /// </para>
    /// </remarks>
    public static ReferenciaRegra Regra(this LeitorEnvelope leitor, JsonObject pai, string chave, string path, params string[] rol)
    {
        if (leitor.Falhou)
        {
            return default!;
        }

        JsonObject objeto = leitor.Objeto(pai, chave, path);
        if (leitor.Falhou)
        {
            return default!;
        }

        return leitor.RegraDoObjeto(objeto, $"{path}.{chave}", rol);
    }

    public static ReferenciaRegra? RegraOpcional(this LeitorEnvelope leitor, JsonObject pai, string chave, string path, params string[] rol)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        JsonObject? objeto = leitor.ObjetoOpcional(pai, chave, path);
        if (leitor.Falhou || objeto is null)
        {
            return null;
        }

        return leitor.RegraDoObjeto(objeto, $"{path}.{chave}", rol);
    }

    /// <summary>
    /// A tripla de um objeto que já <b>é</b> a referência — fecha a gramática do objeto, mede
    /// código e versão contra os tetos das colunas, confere o código contra o
    /// <paramref name="rol"/> conhecido e reconstrói a identidade do value object.
    /// </summary>
    /// <remarks>
    /// Nem todo bloco que carrega uma referência de regra a carrega sozinha: a convenção de
    /// contagem do prazo declara a tripla ao lado da chave de presença. É para essa forma que
    /// existe <paramref name="chavesAlemDaTripla"/> — o chamador declara o que o bloco tem a mais,
    /// e o fechamento continua acontecendo aqui. Deixá-lo a cargo do chamador tornaria a gramática
    /// fechada opcional, e um chamador que esquecesse de fechá-la deixaria passar chave
    /// desconhecida em silêncio, que é como se perde configuração sem ninguém ver.
    /// </remarks>
    public static ReferenciaRegra RegraDoObjeto(
        this LeitorEnvelope leitor,
        JsonObject objeto,
        string path,
        string[] rol,
        params string[] chavesAlemDaTripla)
    {
        leitor.ExigirChaves(objeto, path, [.. chavesAlemDaTripla, "codigo", "versao", "hash"]);

        string codigo = leitor.TextoNaoVazio(objeto, "codigo", path, LimitesDoEnvelope.RegraCodigo);
        string versao = leitor.TextoNaoVazio(objeto, "versao", path, LimitesDoEnvelope.RegraVersao);
        string hash = leitor.Texto(objeto, "hash", path);

        if (leitor.Falhou)
        {
            return default!;
        }

        if (!rol.Contains(codigo, StringComparer.Ordinal))
        {
            return leitor.Registrar<ReferenciaRegra>(
                ErrosCodecEnvelope.RegraDesconhecida,
                $"O código de regra '{codigo}' em '{path}' não pertence ao rol conhecido ({string.Join(", ", rol)}).");
        }

        Result<ReferenciaRegra> referencia = ReferenciaRegra.Criar(codigo, versao, hash);
        if (referencia.IsFailure)
        {
            return leitor.Propagar<ReferenciaRegra>(referencia.Error!);
        }

        return referencia.Value!;
    }
}
