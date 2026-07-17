namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura <b>fechada</b> de um envelope canônico: cada objeto declara o conjunto
/// exato das suas chaves, cada valor é lido na forma exata em que foi escrito, e o
/// primeiro desvio para a leitura inteira.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que a gramática é fechada, e não tolerante.</b> Um leitor que ignora o que
/// não conhece degrada em silêncio — e silêncio, aqui, é configuração perdida. O caso
/// concreto: <c>bonusRegional</c> tem duas formas, <c>{"presente":false}</c> e a
/// completa. Um leitor que só olhasse <c>presente</c> leria
/// <c>{"presente":false,"fator":"1.2000",…}</c> como “sem bônus” e <b>descartaria o
/// bônus do certame</b> sem uma linha de log. Por isso: chave a mais é erro, chave a
/// menos é erro, e um objeto sem args tem de ser exatamente <c>{}</c>.
/// </para>
/// <para>
/// <b>Acumula o primeiro erro em vez de propagar <c>Result</c> a cada leitura.</b> A
/// alternativa — encadear dezenas de <c>Result</c> — produziria um decoder onde o
/// caminho de erro é mais longo que o de sucesso, e é no caminho de erro que os
/// buracos se escondem. O contrato é: o chamador verifica <see cref="Falhou"/> antes
/// de usar qualquer valor lido.
/// </para>
/// </remarks>
internal sealed class LeitorEnvelope
{
    private DomainError? _erro;

    public bool Falhou => _erro is not null;

    public DomainError Erro => _erro ?? throw new InvalidOperationException("Leitura sem erro registrado.");

    public Result<T> Falha<T>() => Result<T>.Failure(Erro);

    private T Registrar<T>(string codigo, string mensagem)
    {
        _erro ??= new DomainError(codigo, mensagem);
        return default!;
    }

    private T Malformado<T>(string path, string detalhe) =>
        Registrar<T>(ErrosCodecEnvelope.EnvelopeMalformado, $"Envelope malformado em '{path}': {detalhe}");

    /// <summary>
    /// Exige que <paramref name="objeto"/> tenha <b>exatamente</b> as chaves esperadas —
    /// nem mais, nem menos. É esta função que fecha a gramática em cada nível.
    /// </summary>
    public void ExigirChaves(JsonObject objeto, string path, params string[] esperadas)
    {
        if (Falhou)
        {
            return;
        }

        HashSet<string> presentes = [.. objeto.Select(static p => p.Key)];

        foreach (string ausente in esperadas.Where(esperada => !presentes.Contains(esperada)))
        {
            Malformado<object>(path, $"a chave '{ausente}' está ausente.");
            return;
        }

        foreach (string intrusa in presentes.Where(presente => !esperadas.Contains(presente, StringComparer.Ordinal)))
        {
            Malformado<object>(path, $"a chave '{intrusa}' não pertence a esta versão do envelope.");
            return;
        }
    }

    /// <summary>
    /// Um bloco (ou sub-chave) que ainda não tem dono — tem de ser exatamente
    /// <c>{"status":"nao_construido"}</c>. Um stub que virou objeto rico é envelope de
    /// outra <b>forma</b>, e forma nova é bump de versão (ADR-0109 D1), não leitura
    /// tolerante.
    /// </summary>
    public void ExigirStub(JsonObject topo, string chave)
    {
        if (Falhou)
        {
            return;
        }

        JsonObject stub = Objeto(topo, chave, chave);
        if (Falhou)
        {
            return;
        }

        ExigirChaves(stub, chave, "status");
        string status = Texto(stub, "status", chave);
        if (!Falhou && status != "nao_construido")
        {
            Malformado<object>($"{chave}.status", $"esperado 'nao_construido', encontrado '{status}'.");
        }
    }

    public JsonObject Objeto(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default!;
        }

        return pai[chave] is JsonObject objeto
            ? objeto
            : Malformado<JsonObject>($"{path}.{chave}", "esperado um objeto.");
    }

    public JsonObject? ObjetoOpcional(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        return node switch
        {
            null => null,
            JsonObject objeto => objeto,
            _ => Malformado<JsonObject?>($"{path}.{chave}", "esperado um objeto ou null."),
        };
    }

    public JsonArray Array(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default!;
        }

        return pai[chave] is JsonArray array
            ? array
            : Malformado<JsonArray>($"{path}.{chave}", "esperado um array.");
    }

    public JsonObject ItemObjeto(JsonArray array, int indice, string path)
    {
        if (Falhou)
        {
            return default!;
        }

        return array[indice] is JsonObject objeto
            ? objeto
            : Malformado<JsonObject>($"{path}[{indice}]", "esperado um objeto.");
    }

    public string Texto(JsonObject pai, string chave, string path, int maxLength = 0)
    {
        if (Falhou)
        {
            return default!;
        }

        string? valor = TextoOpcional(pai, chave, path, maxLength);
        if (Falhou)
        {
            return default!;
        }

        return valor ?? Malformado<string>($"{path}.{chave}", "esperado um texto, encontrado null.");
    }

    /// <summary>
    /// Texto que as factories do domínio exigem <b>não-branco</b> — e o exigem
    /// <b>lançando</b> (<c>ArgumentException.ThrowIfNullOrWhiteSpace</c>), não devolvendo
    /// <c>Result</c>. Sem esta guarda, um envelope com <c>"nome": ""</c> atravessaria a
    /// leitura e explodiria dentro de <c>EtapaProcesso.Reidratar</c> — uma exceção não
    /// tratada no meio de um descarte, que viraria 500 em vez de recusa nomeada.
    /// </summary>
    /// <param name="maxLength">
    /// O limite da <b>coluna</b> que vai receber o valor (<see cref="LimitesDoEnvelope"/>).
    /// Passe <c>0</c> quando o destino não for uma coluna com limite — o caso dos campos
    /// que vivem dentro de um <c>json</c>/<c>jsonb</c>.
    /// </param>
    public string TextoNaoVazio(JsonObject pai, string chave, string path, int maxLength = 0)
    {
        if (Falhou)
        {
            return default!;
        }

        string texto = Texto(pai, chave, path, maxLength);
        if (Falhou)
        {
            return default!;
        }

        return string.IsNullOrWhiteSpace(texto)
            ? Malformado<string>($"{path}.{chave}", "esperado um texto não vazio.")
            : texto;
    }

    /// <summary>
    /// Recusa o texto que <b>não cabe na coluna</b>.
    /// </summary>
    /// <remarks>
    /// O limite não é capricho: um <c>etapas[].nome</c> com 301 caracteres passa por toda a
    /// leitura, satisfaz o domínio, <b>recanonicaliza nos mesmos bytes</b> — o encoder o
    /// reemite tal qual — e a prova de round-trip <b>aprova</b>. A recusa só chega no
    /// <c>SaveChanges</c>, como <c>DbUpdateException</c> do Postgres (22001, <i>value too
    /// long</i>), no meio do descarte: <b>500 não tratado</b> em vez de recusa nomeada. É a
    /// mesma classe de falha que a guarda de não-vazio fecha, e o caminho de comando já a
    /// barra no <c>FluentValidation</c>.
    /// </remarks>
    private string GuardarComprimento(string texto, string path, int maxLength) =>
        maxLength > 0 && texto.Length > maxLength
            ? Malformado<string>(path, $"o texto tem {texto.Length} caracteres e a coluna comporta no máximo {maxLength}.")
            : texto;

    public string? TextoOpcional(JsonObject pai, string chave, string path, int maxLength = 0)
    {
        if (Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        if (node is null)
        {
            return null;
        }

        if (node.GetValueKind() != System.Text.Json.JsonValueKind.String)
        {
            return Malformado<string?>($"{path}.{chave}", "esperado um texto.");
        }

        return GuardarComprimento(node.GetValue<string>(), $"{path}.{chave}", maxLength);
    }

    /// <summary>
    /// Guid na forma <b>exata</b> em que o encoder o escreve: <c>D</c>, minúsculo. Aceitar
    /// maiúsculo ou chaves faria a reserialização produzir bytes distintos dos lidos — o
    /// round-trip acusaria, mas só depois de a leitura já ter dado um valor por bom.
    /// </summary>
    public Guid Identificador(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default;
        }

        string texto = Texto(pai, chave, path);
        if (Falhou)
        {
            return default;
        }

        if (!Guid.TryParseExact(texto, "D", out Guid valor) || !string.Equals(valor.ToString(), texto, StringComparison.Ordinal))
        {
            return Malformado<Guid>($"{path}.{chave}", $"esperado um Guid canônico (minúsculo, formato D), encontrado '{texto}'.");
        }

        // Guid vazio é sintaticamente um Guid, e é aí que ele engana. As factories reagem de
        // dois modos, ambos errados: EtapaProcesso.Reidratar e os filhos do atendimento
        // LANÇAM (500 no meio de um descarte, em vez de recusa nomeada), enquanto
        // ConfiguracaoDistribuicaoVagas.Criar simplesmente o ACEITA como oferta de curso — e
        // o grafo inválido restaura, persiste e faz round-trip perfeito. Nenhum id do
        // envelope pode ser vazio; recusar aqui é a única barreira que vale para todos.
        if (valor == Guid.Empty)
        {
            return Malformado<Guid>($"{path}.{chave}", "o identificador não pode ser o Guid vazio.");
        }

        return valor;
    }

    /// <summary>
    /// Identificador opcional (Story #554, PR-e — <c>DocumentoExigido.GrupoSatisfacaoId</c>/
    /// <c>IdadeMaximaEmissao.ReferenciaFaseId</c>, ambos <c>Guid?</c> por natureza).
    /// <see langword="null"/> quando a chave é <c>null</c>; do contrário, mesma validação
    /// estrita de <see cref="Identificador"/> (formato canônico, nunca o Guid vazio).
    /// </summary>
    public Guid? IdentificadorOpcional(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return null;
        }

        string? texto = TextoOpcional(pai, chave, path);
        if (Falhou || texto is null)
        {
            return null;
        }

        if (!Guid.TryParseExact(texto, "D", out Guid valor) || !string.Equals(valor.ToString(), texto, StringComparison.Ordinal))
        {
            return Malformado<Guid?>($"{path}.{chave}", $"esperado um Guid canônico (minúsculo, formato D), encontrado '{texto}'.");
        }

        return valor == Guid.Empty
            ? Malformado<Guid?>($"{path}.{chave}", "o identificador não pode ser o Guid vazio.")
            : valor;
    }

    /// <summary>
    /// Data no formato canônico — e <b>nunca</b> o <c>default(DateOnly)</c>.
    /// </summary>
    /// <remarks>
    /// <c>0001-01-01</c> é como um campo de data <b>omitido</b> se materializa, e é por isso
    /// que os validators de publicar e de retificar o recusam explicitamente
    /// (<c>NotEqual(default(DateOnly))</c>). <c>DadosEdital.Criar</c>, porém, só checa
    /// <c>fim &gt;= inicio</c> — e <c>0001-01-01</c> satisfaz isso. Como o encoder reemite a
    /// data tal qual, um envelope com o período zerado faria round-trip <b>perfeito</b> e
    /// restauraria um certame cujo período de inscrição é impossível.
    /// </remarks>
    public DateOnly Data(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default;
        }

        string texto = Texto(pai, chave, path);
        if (Falhou)
        {
            return default;
        }

        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly valor))
        {
            return Malformado<DateOnly>($"{path}.{chave}", $"esperada uma data 'yyyy-MM-dd', encontrado '{texto}'.");
        }

        return valor == default
            ? Malformado<DateOnly>($"{path}.{chave}", "a data não pode ser o valor default (0001-01-01) — é assim que uma data omitida se materializa.")
            : valor;
    }

    /// <summary>
    /// Data opcional no formato canônico (Story #853, <c>ObrigatoriedadeLegal.VigenciaFim</c>
    /// — vigência aberta é estado válido). <see langword="null"/> quando a chave é
    /// <c>null</c>; do contrário, mesma validação estrita de <see cref="Data"/> (nunca o
    /// valor default).
    /// </summary>
    public DateOnly? DataOpcional(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return null;
        }

        string? texto = TextoOpcional(pai, chave, path);
        if (Falhou || texto is null)
        {
            return null;
        }

        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly valor))
        {
            return Malformado<DateOnly?>($"{path}.{chave}", $"esperada uma data 'yyyy-MM-dd', encontrado '{texto}'.");
        }

        return valor == default
            ? Malformado<DateOnly?>($"{path}.{chave}", "a data não pode ser o valor default (0001-01-01) — é assim que uma data omitida se materializa.")
            : valor;
    }

    /// <summary>
    /// Instante opcional na forma canônica (RFC 3339, UTC, sem fração —
    /// <see cref="HashCanonicalComputer.SerializeInstantCanonical"/>), usado pela janela da
    /// <c>FaseCronograma</c> (Story #851, CA-07). <see langword="null"/> quando a chave é
    /// <c>null</c> — "sem data" é estado válido para fase de origem delegada.
    /// </summary>
    public DateTimeOffset? InstanteOpcional(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return null;
        }

        string? texto = TextoOpcional(pai, chave, path);
        if (Falhou || texto is null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParseExact(
                texto, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset valor))
        {
            return Malformado<DateTimeOffset?>(
                $"{path}.{chave}", $"esperado um instante canônico ('yyyy-MM-ddTHH:mm:ssZ'), encontrado '{texto}'.");
        }

        if (!string.Equals(HashCanonicalComputer.SerializeInstantCanonical(valor), texto, StringComparison.Ordinal))
        {
            return Malformado<DateTimeOffset?>($"{path}.{chave}", "o instante não está na forma canônica.");
        }

        return valor;
    }

    public bool Booleano(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default;
        }

        JsonNode? node = pai[chave];
        System.Text.Json.JsonValueKind? kind = node?.GetValueKind();

        return kind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
            ? node!.GetValue<bool>()
            : Malformado<bool>($"{path}.{chave}", "esperado um booleano.");
    }

    public int Inteiro(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default;
        }

        int? valor = InteiroOpcional(pai, chave, path);
        if (Falhou)
        {
            return default;
        }

        return valor ?? Malformado<int>($"{path}.{chave}", "esperado um inteiro, encontrado null.");
    }

    public int? InteiroOpcional(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        if (node is null)
        {
            return null;
        }

        if (node.GetValueKind() != System.Text.Json.JsonValueKind.Number || !node.AsValue().TryGetValue(out int valor))
        {
            return Malformado<int?>($"{path}.{chave}", "esperado um inteiro.");
        }

        return valor;
    }

    /// <summary>
    /// Decimal de negócio — escrito pelo encoder como <b>string</b> com escala declarada
    /// (ADR-0100 item 2), nunca como número JSON. A leitura exige que a reserialização
    /// com a <b>mesma escala</b> reproduza a string lida: <c>"1.00000"</c> ou
    /// <c>"1.0"</c> não são a mesma coisa que <c>"1.0000"</c>, e aceitá-los deixaria a
    /// escala do envelope à mercê de quem o escreveu por fora.
    /// </summary>
    public decimal Decimal(JsonObject pai, string chave, int escala, string path, int precisao = 0)
    {
        if (Falhou)
        {
            return default;
        }

        decimal? valor = DecimalOpcional(pai, chave, escala, path, precisao);
        if (Falhou)
        {
            return default;
        }

        return valor ?? Malformado<decimal>($"{path}.{chave}", "esperado um decimal, encontrado null.");
    }

    /// <param name="precisao">
    /// Total de dígitos que a <b>coluna</b> comporta (o <c>p</c> de <c>numeric(p,s)</c> —
    /// ver <see cref="LimitesDoEnvelope"/>). Passe <c>0</c> quando o destino não for uma
    /// coluna com precisão declarada (os decimais que vivem dentro de um <c>json</c>).
    /// </param>
    public decimal? DecimalOpcional(JsonObject pai, string chave, int escala, string path, int precisao = 0)
    {
        if (Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        if (node is null)
        {
            return null;
        }

        string? texto = TextoOpcional(pai, chave, path);
        if (Falhou)
        {
            return null;
        }

        if (!decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal valor)
            || !string.Equals(HashCanonicalComputer.SerializeDecimalCanonical(valor, escala), texto, StringComparison.Ordinal))
        {
            return Malformado<decimal?>($"{path}.{chave}", $"esperado um decimal canônico com {escala} casas, encontrado '{texto}'.");
        }

        // A escala sozinha não basta. `numeric(18,4)` também limita o total de dígitos, e um
        // peso como "99999999999999999999.0000" tem escala 4 impecável — recanonicaliza nos
        // MESMOS bytes, satisfaz o domínio, e a prova de round-trip aprova. O estouro só
        // aflora no SaveChanges, como 22003 (numeric field overflow) no meio do descarte:
        // 500 não tratado em vez de recusa nomeada.
        if (precisao > 0 && DigitosSignificativos(valor) > precisao)
        {
            return Malformado<decimal?>(
                $"{path}.{chave}",
                $"o decimal '{texto}' tem mais dígitos do que a coluna comporta (numeric({precisao},{escala})).");
        }

        return valor;
    }

    /// <summary>Dígitos totais de um decimal já arredondado à escala do campo — o <c>p</c> de <c>numeric(p,s)</c>.</summary>
    private static int DigitosSignificativos(decimal valor)
    {
        string digitos = Math.Abs(valor).ToString(CultureInfo.InvariantCulture).Replace(".", string.Empty, StringComparison.Ordinal);
        string semZerosAEsquerda = digitos.TrimStart('0');

        return semZerosAEsquerda.Length == 0 ? 1 : semZerosAEsquerda.Length;
    }

    /// <summary>
    /// Enum pelo <b>nome exato</b> — o encoder escreve <c>ToString()</c>. Aceitar
    /// case-insensitive faria dois envelopes textualmente distintos reidratarem no mesmo
    /// valor, e o round-trip acusaria uma divergência que a leitura já deveria ter
    /// recusado.
    /// </summary>
    public TEnum Enumeracao<TEnum>(JsonObject pai, string chave, string path)
        where TEnum : struct, Enum
    {
        if (Falhou)
        {
            return default;
        }

        string texto = Texto(pai, chave, path);
        if (Falhou)
        {
            return default;
        }

        if (!Enum.TryParse(texto, ignoreCase: false, out TEnum valor) || !Enum.IsDefined(valor))
        {
            return Malformado<TEnum>($"{path}.{chave}", $"valor '{texto}' não é um {typeof(TEnum).Name} conhecido.");
        }

        return valor;
    }

    /// <summary>
    /// Variante opcional de <see cref="Enumeracao{TEnum}"/> — usada pelos pares de
    /// suspensividade de <c>ArgsRegraPrazoRecurso</c> (Story #851), em que
    /// <see langword="null"/> é valor legítimo ("esta instância não bloqueia").
    /// </summary>
    public TEnum? EnumeracaoOpcional<TEnum>(JsonObject pai, string chave, string path)
        where TEnum : struct, Enum
    {
        if (Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        return node is null ? null : Enumeracao<TEnum>(pai, chave, path);
    }

    /// <summary>
    /// Array de escalares de texto — cada item <b>não-branco</b> e <b>já normalizado</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Um item em branco em <c>criteriosCumulativos</c> é um <b>critério de elegibilidade
    /// que não diz nada</b>; um item com espaços nas pontas (<c>" PCD "</c>) é pior: ele é
    /// um critério que <b>ninguém reconhece</b>. Os dois entram no jsonb, fazem round-trip
    /// perfeito (o encoder reemite o array item a item, verbatim) e viram requisitos que o
    /// motor de homologação teria de avaliar por <b>comparação exata</b>. O cadastro nunca
    /// os produz — ele normaliza os critérios (<c>Modalidade.NormalizarCriterios</c>).
    /// </para>
    /// <para>
    /// <b>A guarda recusa; ela não normaliza.</b> Aparar o espaço aqui mudaria o valor em
    /// relação aos bytes congelados, a recanonicalização produziria bytes distintos, e a
    /// prova de round-trip <b>recusaria o descarte de um certame legítimo</b> — trocando um
    /// dado sujo por um certame sem descarte. O envelope ou está na forma que o cadastro
    /// produz, ou não é reidratável.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Textos(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return [];
        }

        JsonArray array = Array(pai, chave, path);
        if (Falhou)
        {
            return [];
        }

        List<string> valores = [];
        for (int i = 0; i < array.Count; i++)
        {
            JsonNode? item = array[i];
            if (item is null || item.GetValueKind() != System.Text.Json.JsonValueKind.String)
            {
                return Malformado<IReadOnlyList<string>>($"{path}.{chave}[{i}]", "esperado um texto.") ?? [];
            }

            string valor = item.GetValue<string>();
            if (string.IsNullOrWhiteSpace(valor))
            {
                return Malformado<IReadOnlyList<string>>($"{path}.{chave}[{i}]", "esperado um texto não vazio.") ?? [];
            }

            if (!string.Equals(valor, valor.Trim(), StringComparison.Ordinal))
            {
                return Malformado<IReadOnlyList<string>>(
                    $"{path}.{chave}[{i}]",
                    $"esperado um texto já normalizado (sem espaços nas pontas), encontrado '{valor}'.") ?? [];
            }

            valores.Add(valor);
        }

        return valores;
    }

    /// <summary>
    /// Valor JSON bruto — escalar ou array, na forma exata em que foi escrito —
    /// usado pelos campos que o Domain trata como <see cref="System.Text.Json.JsonElement"/>
    /// (<c>CondicaoDnf.Valor</c>, ADR-0111). Sem interpretação estrutural nesta
    /// camada: a forma é conferida pela factory do domínio (<c>CondicaoDnf.Criar</c>).
    /// </summary>
    public System.Text.Json.JsonElement Valor(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default;
        }

        JsonNode? node = pai[chave];
        if (node is null)
        {
            return Malformado<System.Text.Json.JsonElement>($"{path}.{chave}", "esperado um valor, encontrado null.");
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
    /// hoje (RN08).
    /// </para>
    /// <para>
    /// O que resta verificável <b>sem</b> estado vivo é o que se verifica: o código pertence
    /// ao rol da forma <c>1.1</c>, a versão e o código não são brancos e cabem nas colunas, e
    /// o hash tem o shape content-addressable (<c>ReferenciaRegra.Criar</c>). A prova de que
    /// a tripla <i>existiu</i> é o hash do <b>envelope</b>, que cobre os três campos.
    /// </para>
    /// </remarks>
    public ReferenciaRegra Regra(JsonObject pai, string chave, string path, params string[] rol)
    {
        if (Falhou)
        {
            return default!;
        }

        JsonObject objeto = Objeto(pai, chave, path);
        if (Falhou)
        {
            return default!;
        }

        return RegraDoObjeto(objeto, $"{path}.{chave}", rol);
    }

    public ReferenciaRegra? RegraOpcional(JsonObject pai, string chave, string path, params string[] rol)
    {
        if (Falhou)
        {
            return null;
        }

        JsonObject? objeto = ObjetoOpcional(pai, chave, path);
        if (Falhou || objeto is null)
        {
            return null;
        }

        return RegraDoObjeto(objeto, $"{path}.{chave}", rol);
    }

    private ReferenciaRegra RegraDoObjeto(JsonObject objeto, string path, string[] rol)
    {
        ExigirChaves(objeto, path, "codigo", "versao", "hash");

        string codigo = TextoNaoVazio(objeto, "codigo", path, LimitesDoEnvelope.RegraCodigo);
        string versao = TextoNaoVazio(objeto, "versao", path, LimitesDoEnvelope.RegraVersao);
        string hash = Texto(objeto, "hash", path);

        if (Falhou)
        {
            return default!;
        }

        if (!rol.Contains(codigo, StringComparer.Ordinal))
        {
            return Registrar<ReferenciaRegra>(
                ErrosCodecEnvelope.RegraDesconhecida,
                $"O código de regra '{codigo}' em '{path}' não pertence ao rol conhecido ({string.Join(", ", rol)}).");
        }

        Result<ReferenciaRegra> referencia = ReferenciaRegra.Criar(codigo, versao, hash);
        if (referencia.IsFailure)
        {
            return Propagar<ReferenciaRegra>(referencia.Error!);
        }

        return referencia.Value!;
    }

    /// <summary>
    /// Propaga o <see cref="DomainError"/> de uma factory do domínio como está — as
    /// invariantes do agregado já <b>são</b> a validação; reescrevê-las aqui criaria uma
    /// segunda fonte de verdade que envelheceria em silêncio.
    /// </summary>
    public T Propagar<T>(DomainError erro)
    {
        _erro ??= erro;
        return default!;
    }
}
