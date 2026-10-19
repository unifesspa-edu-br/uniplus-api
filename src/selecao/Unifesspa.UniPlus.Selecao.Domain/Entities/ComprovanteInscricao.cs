namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O comprovante de uma submissão da <see cref="Inscricao"/> (issue #1858, §4.1 do plano da
/// #1855): cópia do que foi enviado, SEM nenhum número de documento, com o hash de integridade
/// que prova que o comprovante não mudou. O hash é o SHA-256 do JSON canônico do conteúdo
/// (número, instante em segundos UTC, oferta, respostas, derivados, grupos, aceites e, quando a
/// versão deriva a modalidade, as modalidades em que o candidato concorre; e, quando a inscrição
/// declara a 2ª opção de curso, a oferta dela e as modalidades dela); qualquer um o
/// recalcula, e não é um código de autenticação.
/// </summary>
/// <remarks>
/// <para>
/// Implementa <see cref="IForensicEntity"/> per ADR-0063: append-only, NÃO herda
/// <see cref="Kernel.Domain.Entities.EntityBase"/> e não carrega soft-delete. Uma correção gera um
/// comprovante NOVO; o anterior nunca muda.
/// </para>
/// <para>
/// As respostas de cifra individual (identidade de gênero e orientação sexual) ficam fora do hash e
/// do JSON em claro: um valor de poucas opções, hasheado junto do conteúdo, seria recuperável por
/// tentativa. Vão à parte em <see cref="RespostasSensiveis"/>, cifradas pelo repositório. O AES-GCM
/// garante que o envelope não foi alterado, mas não a que comprovante ele pertence: o HMAC de id e
/// valores, em <see cref="SensiveisHmac"/>, prende um ao outro. O hash cobre os códigos dos fatos
/// sensíveis, nunca os valores, para que apagar as colunas deles seja detectado.
/// </para>
/// </remarks>
public sealed class ComprovanteInscricao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid InscricaoId { get; private init; }

    public string NumeroInscricao { get; private init; } = string.Empty;

    public DateTimeOffset Instante { get; private init; }

    public string HashIntegridade { get; private init; } = string.Empty;

    /// <summary>A oferta de curso da 1ª opção.</summary>
    public Guid? OfertaCursoId { get; private init; }

    /// <summary>A oferta de curso da 2ª opção; nula quando a inscrição não a declarou nesta submissão.</summary>
    public Guid? OfertaCursoSegundaOpcaoId { get; private init; }

    /// <summary>O que o candidato informou, no momento da submissão — sem números de documento.</summary>
    public IReadOnlyDictionary<string, JsonElement> Respostas { get; private init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>O que o sistema derivou das respostas nesta submissão.</summary>
    public IReadOnlyDictionary<string, JsonElement> Derivados { get; private init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>As ocorrências de grupo repetível no momento da submissão — mesma exclusão de documento que <see cref="Respostas"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos { get; private init; } =
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal);

    public IReadOnlyList<AceiteDeTermo> Aceites { get; private init; } = [];

    /// <summary>
    /// As respostas de cifra individual desta submissão, em claro só em memória: o repositório as cifra
    /// ao gravar (<see cref="SensiveisCifrados"/>) e as devolve ao ler. Fora do hash.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> RespostasSensiveis { get; private set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>As respostas de cifra individual como o banco as guarda: cifradas, nulo quando não há.</summary>
    public byte[]? SensiveisCifrados { get; private set; }

    /// <summary>
    /// O HMAC do id deste comprovante e dos valores sensíveis, que prende o envelope cifrado a ele: o
    /// envelope copiado de outro comprovante não confere.
    /// </summary>
    public byte[]? SensiveisHmac { get; private set; }

    /// <summary>
    /// As modalidades em que o candidato concorre nesta submissão, com o nome congelado na publicação e
    /// a ordem das ofertadas (UNI-REQ-0076); nulo quando a versão fixada não deriva a modalidade.
    /// </summary>
    public IReadOnlyList<ModalidadeDeConcorrencia>? Modalidades { get; private init; }

    /// <summary>
    /// As modalidades em que o candidato concorre na 2ª opção, calculadas sobre a distribuição de vagas
    /// da oferta dela; nulo quando não há 2ª opção ou a versão fixada não deriva a modalidade.
    /// </summary>
    public IReadOnlyList<ModalidadeDeConcorrencia>? ModalidadesSegundaOpcao { get; private init; }

    // Construtor de materialização do EF Core.
    private ComprovanteInscricao() { }

    public static ComprovanteInscricao Emitir(
        Guid inscricaoId,
        string numeroInscricao,
        DateTimeOffset instante,
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        IReadOnlyList<ModalidadeDeConcorrencia>? modalidades = null,
        IReadOnlyDictionary<string, JsonElement>? respostasSensiveis = null,
        Guid? ofertaCursoSegundaOpcaoId = null,
        IReadOnlyList<ModalidadeDeConcorrencia>? modalidadesSegundaOpcao = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroInscricao);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(derivados);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);

        Dictionary<string, JsonElement> respostasGravadas = NaFormaGravada(respostas);
        Dictionary<string, JsonElement> derivadosGravados = NaFormaGravada(derivados);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> gruposGravados = grupos.ToDictionary(
            static g => g.Key,
            static g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                [.. g.Value.Select(static o => o with { Respostas = NaFormaGravada(o.Respostas) })],
            StringComparer.Ordinal);

        return new ComprovanteInscricao
        {
            InscricaoId = inscricaoId,
            NumeroInscricao = numeroInscricao,
            Instante = instante,
            HashIntegridade = HashDoConteudo(
                numeroInscricao, instante, ofertaCursoId, respostasGravadas, derivadosGravados, gruposGravados, aceites, modalidades,
                (respostasSensiveis ?? new Dictionary<string, JsonElement>()).Keys, ofertaCursoSegundaOpcaoId, modalidadesSegundaOpcao),
            Grupos = gruposGravados,
            OfertaCursoId = ofertaCursoId,
            OfertaCursoSegundaOpcaoId = ofertaCursoSegundaOpcaoId,
            Respostas = respostasGravadas,
            Derivados = derivadosGravados,
            Aceites = aceites,
            Modalidades = modalidades,
            ModalidadesSegundaOpcao = modalidadesSegundaOpcao,
            RespostasSensiveis = NaFormaGravada(respostasSensiveis),
        };
    }

    /// <summary>Guarda as respostas de cifra individual cifradas; só o repositório, ao gravar, chama.</summary>
    public void GuardarSensiveisCifrados(byte[]? cifrados, byte[]? hmac)
    {
        SensiveisCifrados = cifrados;
        SensiveisHmac = hmac;
    }

    /// <summary>Devolve as respostas de cifra individual decifradas; só o repositório, ao ler, chama.</summary>
    public void RestaurarSensiveis(IReadOnlyDictionary<string, JsonElement> decifradas)
    {
        ArgumentNullException.ThrowIfNull(decifradas);
        RespostasSensiveis = decifradas;
    }

    /// <summary>Recalcula o hash a partir do conteúdo, como faria quem audita o comprovante.</summary>
    public bool IntegridadeConfere() =>
        string.Equals(
            HashIntegridade,
            HashDoConteudo(
                NumeroInscricao, Instante, OfertaCursoId, Respostas, Derivados, Grupos, Aceites, Modalidades, RespostasSensiveis.Keys,
                OfertaCursoSegundaOpcaoId, ModalidadesSegundaOpcao),
            StringComparison.Ordinal);

    /// <summary>
    /// O banco guarda o número do JSON em forma decimal, sem expoente (<c>1e3</c> volta como <c>1000</c>). O
    /// comprovante adota essa forma antes do hash, para que o hash recalculado do que foi gravado seja o mesmo.
    /// </summary>
    private static Dictionary<string, JsonElement> NaFormaGravada(IReadOnlyDictionary<string, JsonElement>? valores) =>
        (valores ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal))
            .ToDictionary(static v => v.Key, static v => NaFormaGravada(v.Value), StringComparer.Ordinal);

    private static JsonElement NaFormaGravada(JsonElement valor)
    {
        using MemoryStream destino = new();
        using (Utf8JsonWriter escritor = new(destino))
        {
            Escrever(valor, escritor);
        }

        using JsonDocument documento = JsonDocument.Parse(destino.ToArray());
        return documento.RootElement.Clone();
    }

    private static void Escrever(JsonElement valor, Utf8JsonWriter escritor)
    {
        switch (valor.ValueKind)
        {
            case JsonValueKind.Object:
                escritor.WriteStartObject();
                foreach (JsonProperty propriedade in valor.EnumerateObject())
                {
                    escritor.WritePropertyName(propriedade.Name);
                    Escrever(propriedade.Value, escritor);
                }

                escritor.WriteEndObject();
                break;
            case JsonValueKind.Array:
                escritor.WriteStartArray();
                foreach (JsonElement item in valor.EnumerateArray())
                {
                    Escrever(item, escritor);
                }

                escritor.WriteEndArray();
                break;
            case JsonValueKind.Number when valor.TryGetDecimal(out decimal numero):
                escritor.WriteNumberValue(numero);
                break;
            default:
                valor.WriteTo(escritor);
                break;
        }
    }

    private static string HashDoConteudo(
        string numeroInscricao,
        DateTimeOffset instante,
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        IReadOnlyList<ModalidadeDeConcorrencia>? modalidades,
        IEnumerable<string> codigosSensiveis,
        Guid? ofertaCursoSegundaOpcaoId,
        IReadOnlyList<ModalidadeDeConcorrencia>? modalidadesSegundaOpcao)
    {
        JsonObject conteudo = new()
        {
            ["numeroInscricao"] = numeroInscricao,
            ["instante"] = HashCanonicalComputer.SerializeInstantCanonical(instante),
            ["ofertaCursoId"] = ofertaCursoId?.ToString(),
            ["respostas"] = Nodo(respostas),
            ["derivados"] = Nodo(derivados),
            ["grupos"] = Nodo(grupos),
            ["aceites"] = Nodo(aceites),
        };

        if (modalidades is not null)
        {
            conteudo["modalidades"] = Nodo(modalidades);
        }

        // A 2ª opção só entra quando declarada: o comprovante sem ela segue com o hash que sempre teve.
        if (ofertaCursoSegundaOpcaoId is { } segundaOpcao)
        {
            conteudo["ofertaCursoSegundaOpcaoId"] = segundaOpcao.ToString();
        }

        if (modalidadesSegundaOpcao is not null)
        {
            conteudo["modalidadesSegundaOpcao"] = Nodo(modalidadesSegundaOpcao);
        }

        // Só os códigos, nunca os valores: apagar as colunas dos sensíveis muda o hash, e o comprovante
        // sem sensíveis segue com o hash que sempre teve.
        string[] codigos = [.. codigosSensiveis.Order(StringComparer.Ordinal)];
        if (codigos.Length > 0)
        {
            conteudo["sensiveis"] = Nodo(codigos);
        }

        return HashCanonicalComputer.ComputeSha256Hex(HashCanonicalComputer.ComputeSnapshotBytes(conteudo));
    }

    private static JsonNode? Nodo<T>(T valor) => JsonSerializer.SerializeToNode(valor, HashCanonicalComputer.CanonicalOptions);
}
