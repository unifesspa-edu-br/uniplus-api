namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Collections.Frozen;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Restrição sobre o valor que o candidato responde num item do formulário (UNI-REQ-0145).
/// <see cref="Avaliar"/> diz se a resposta a atende: <see cref="Ternario.Verdadeiro"/> quando
/// atende, <see cref="Ternario.Falso"/> quando viola, e <see cref="Ternario.Indeterminado"/> quando
/// a restrição depende de uma resposta anterior que ainda não se sabe.
/// </summary>
/// <remarks>
/// São descrições de configuração já validada pelo módulo dono: as invariantes são protegidas com
/// <see cref="ArgumentException"/>, porque um valor inválido aqui é defeito de quem monta a
/// descrição, não recusa ao administrador.
/// </remarks>
public abstract record RestricaoValor
{
    private protected RestricaoValor()
    {
    }

    public abstract TipoRestricaoValor Tipo { get; }

    /// <summary>Os fatos de que a restrição depende — entram como dependência do item no grafo.</summary>
    public virtual IReadOnlyCollection<string> FatosCitados => [];

    /// <summary>Avalia a resposta, já sabida não vazia, contra a restrição.</summary>
    public abstract Ternario Avaliar(JsonElement resposta, IReadOnlyDictionary<string, FatoResolvido> fatos);

    /// <summary>
    /// As opções que a restrição deixa escolher diante das respostas anteriores; nula quando ela não
    /// limita a escolha a um conjunto que o servidor enumera.
    /// </summary>
    public virtual OpcoesVigentes? Opcoes(IReadOnlyDictionary<string, FatoResolvido> fatos) => null;

    private protected static Ternario ComoTernario(bool atende) => atende ? Ternario.Verdadeiro : Ternario.Falso;
}

/// <summary>A resposta numérica fica entre um mínimo e um máximo, inclusive; um dos limites pode faltar.</summary>
public sealed record FaixaNumerica : RestricaoValor
{
    public FaixaNumerica(decimal? minimo, decimal? maximo)
    {
        if (Violacao(minimo, maximo) is { } violacao)
        {
            throw new ArgumentException(violacao);
        }

        Minimo = minimo;
        Maximo = maximo;
    }

    public override TipoRestricaoValor Tipo => TipoRestricaoValor.FaixaNumerica;

    public decimal? Minimo { get; }

    public decimal? Maximo { get; }

    public override Ternario Avaliar(JsonElement resposta, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        if (resposta.ValueKind != JsonValueKind.Number || !resposta.TryGetDecimal(out decimal valor))
        {
            return Ternario.Falso;
        }

        return ComoTernario((Minimo is null || valor >= Minimo) && (Maximo is null || valor <= Maximo));
    }

    internal static string? Violacao(decimal? minimo, decimal? maximo) => (minimo, maximo) switch
    {
        (null, null) => "A faixa numérica precisa de ao menos um limite.",
        ({ } min, { } max) when min > max => $"O mínimo ({min}) é maior que o máximo ({max}).",
        _ => null,
    };
}

/// <summary>A resposta de texto tem de um mínimo a um máximo de caracteres; um dos limites pode faltar.</summary>
public sealed record TamanhoTexto : RestricaoValor
{
    public TamanhoTexto(int? minimo, int? maximo)
    {
        if (Violacao(minimo, maximo) is { } violacao)
        {
            throw new ArgumentException(violacao);
        }

        Minimo = minimo;
        Maximo = maximo;
    }

    public override TipoRestricaoValor Tipo => TipoRestricaoValor.TamanhoTexto;

    public int? Minimo { get; }

    public int? Maximo { get; }

    public override Ternario Avaliar(JsonElement resposta, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        if (resposta.ValueKind != JsonValueKind.String)
        {
            return Ternario.Falso;
        }

        int tamanho = resposta.GetString()!.Trim().Length;
        return ComoTernario((Minimo is null || tamanho >= Minimo) && (Maximo is null || tamanho <= Maximo));
    }

    internal static string? Violacao(int? minimo, int? maximo) => (minimo, maximo) switch
    {
        (null, null) => "O tamanho de texto precisa de ao menos um limite.",
        _ when minimo < 0 || maximo < 0 || minimo > maximo => $"Os limites de tamanho ({minimo}, {maximo}) são incoerentes.",
        _ => null,
    };
}

/// <summary>
/// A resposta escolhe só entre as opções vigentes, que são a união das entradas cuja condição é
/// verdadeira. Uma entrada sem condição vale sempre (subconjunto fixo de valores); uma entrada com
/// condição filtra as opções por resposta anterior (UNI-REQ-0145).
/// </summary>
public sealed record OpcoesPermitidas : RestricaoValor
{
    public OpcoesPermitidas(IReadOnlyList<OpcoesCondicionadas> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        if (entradas.Count == 0)
        {
            throw new ArgumentException("As opções permitidas precisam de ao menos uma entrada.", nameof(entradas));
        }

        Entradas = [.. entradas];
    }

    public override TipoRestricaoValor Tipo => TipoRestricaoValor.OpcoesPermitidas;

    /// <summary>Todos os valores que alguma entrada pode permitir.</summary>
    public IReadOnlySet<string> ValoresCitados => Entradas.SelectMany(static e => e.Valores).ToFrozenSet(StringComparer.Ordinal);

    public IReadOnlyList<OpcoesCondicionadas> Entradas { get; }

    public override IReadOnlyCollection<string> FatosCitados =>
        [.. Entradas.SelectMany(static e => e.Quando?.FatosCitados ?? []).Distinct(StringComparer.Ordinal)];

    public override Ternario Avaliar(JsonElement resposta, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        (HashSet<string> vigentes, HashSet<string> talvezVigentes) = Vigentes(fatos);
        return EscolheSoEntre(resposta, vigentes, talvezVigentes);
    }

    public override OpcoesVigentes? Opcoes(IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        (HashSet<string> vigentes, HashSet<string> talvezVigentes) = Vigentes(fatos);
        // As opções são uma união: a entrada que talvez valha só muda o conjunto se trouxer opção nova.
        return new OpcoesVigentes(vigentes, definitivas: talvezVigentes.All(vigentes.Contains));
    }

    /// <summary>
    /// As opções das entradas cuja condição é verdadeira, e as das que ainda dependem de resposta
    /// desconhecida e talvez valham.
    /// </summary>
    private (HashSet<string> Vigentes, HashSet<string> TalvezVigentes) Vigentes(IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        HashSet<string> vigentes = new(StringComparer.Ordinal);
        HashSet<string> talvezVigentes = new(StringComparer.Ordinal);
        foreach (OpcoesCondicionadas entrada in Entradas)
        {
            switch (entrada.Quando?.Avaliar(fatos) ?? Ternario.Verdadeiro)
            {
                case Ternario.Verdadeiro:
                    vigentes.UnionWith(entrada.Valores);
                    break;
                case Ternario.Indeterminado:
                    talvezVigentes.UnionWith(entrada.Valores);
                    break;
                case Ternario.Falso:
                default:
                    break;
            }
        }

        return (vigentes, talvezVigentes);
    }

    /// <summary>
    /// Decide a resposta contra as opções vigentes e as que talvez valham. As opções são uma união,
    /// então uma dependência ainda desconhecida só pode <b>acrescentar</b> opções: a resposta dentro
    /// das vigentes vale em definitivo, a que fica fora até das que talvez valham viola em
    /// definitivo, e só o que depende das desconhecidas fica indeterminado.
    /// </summary>
    internal static Ternario EscolheSoEntre(
        JsonElement resposta, IReadOnlySet<string> vigentes, IReadOnlySet<string> talvezVigentes)
    {
        if (RespostaDeCampo.Codigos(resposta) is not { } codigos)
        {
            return Ternario.Falso;
        }

        if (codigos.All(vigentes.Contains))
        {
            return Ternario.Verdadeiro;
        }

        return codigos.All(codigo => vigentes.Contains(codigo) || talvezVigentes.Contains(codigo))
            ? Ternario.Indeterminado
            : Ternario.Falso;
    }
}

/// <summary>Um grupo de opções e a condição, opcional, em que ele vale.</summary>
public sealed record OpcoesCondicionadas
{
    public OpcoesCondicionadas(PredicadoDnf? quando, IReadOnlyCollection<string> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        if (Violacao(valores) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(valores));
        }

        Quando = quando;
        Valores = valores.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>A condição em que o grupo vale — nula quando vale sempre.</summary>
    public PredicadoDnf? Quando { get; }

    public IReadOnlySet<string> Valores { get; }

    internal static string? Violacao(IReadOnlyCollection<string> valores) =>
        valores.Count == 0 || valores.Any(string.IsNullOrWhiteSpace) ? "Um grupo de opções precisa de valores não vazios." : null;
}

/// <summary>
/// A resposta escolhe só entre as respostas dadas a itens anteriores — por exemplo, a opção de curso
/// da lista de espera entre as opções de curso que o candidato marcou (UNI-REQ-0145).
/// </summary>
/// <remarks>
/// Um fato citado não aplicável ou não informado não contribui com opção; só um fato citado ainda
/// indeterminado deixa as opções indeterminadas. Assim, desmarcar a opção anterior faz a resposta
/// deste item deixar de valer em vez de ficar pendente.
/// </remarks>
public sealed record OpcoesDasRespostas : RestricaoValor
{
    public OpcoesDasRespostas(IReadOnlyCollection<string> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        if (Violacao(fatos) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(fatos));
        }

        Fatos = [.. fatos.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    public override TipoRestricaoValor Tipo => TipoRestricaoValor.OpcoesDasRespostas;

    /// <summary>Os fatos cujas respostas formam as opções, sem repetição e em ordem ordinal.</summary>
    public IReadOnlyList<string> Fatos { get; }

    public override IReadOnlyCollection<string> FatosCitados => Fatos;

    public override Ternario Avaliar(JsonElement resposta, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        OpcoesVigentes opcoes = Opcoes(fatos)!;
        Ternario resultado = OpcoesPermitidas.EscolheSoEntre(
            resposta, opcoes.Codigos.ToHashSet(StringComparer.Ordinal), talvezVigentes: new HashSet<string>(StringComparer.Ordinal));

        // Uma resposta ainda desconhecida pode trazer qualquer opção: o que não está entre as
        // conhecidas fica indeterminado, não violado — salvo a resposta de forma inválida.
        return resultado == Ternario.Falso && !opcoes.Definitivas && RespostaDeCampo.Codigos(resposta) is not null
            ? Ternario.Indeterminado
            : resultado;
    }

    /// <summary>
    /// As respostas resolvidas dos fatos citados; o fato não aplicável ou não informado não contribui,
    /// e o ainda indeterminado deixa as opções em aberto.
    /// </summary>
    public override OpcoesVigentes? Opcoes(IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        HashSet<string> vigentes = new(StringComparer.Ordinal);
        bool definitivas = true;
        foreach (string codigo in Fatos)
        {
            if (!fatos.TryGetValue(codigo, out FatoResolvido? fato) || fato is null || fato.Estado == EstadoFato.Indeterminado)
            {
                definitivas = false;
            }
            else if (fato.Estado == EstadoFato.Resolvido && RespostaDeCampo.Codigos(fato.Valor!.Value) is { } codigos)
            {
                vigentes.UnionWith(codigos);
            }
        }

        return new OpcoesVigentes(vigentes, definitivas);
    }

    internal static string? Violacao(IReadOnlyCollection<string> fatos) =>
        fatos.Count == 0 || fatos.Any(string.IsNullOrWhiteSpace) ? "As opções formadas pelas respostas precisam citar ao menos um fato." : null;
}

/// <summary>
/// A resposta é um município da UF respondida no item <see cref="FatoUf"/>, conferido pelo prefixo
/// do código IBGE (UNI-REQ-0145). A lista de municípios vem do Geo no cliente; o servidor confere
/// a forma e a UF (ADR-0096).
/// </summary>
/// <remarks>
/// Como nas opções formadas pelas respostas, a UF não aplicável ou não informada não admite
/// município, e a UF ainda indeterminada deixa a conferência indeterminada. Trocar a UF faz a
/// resposta deixar de valer em vez de ficar pendente.
/// </remarks>
public sealed record MunicipiosDaUf : RestricaoValor
{
    public MunicipiosDaUf(string fatoUf)
    {
        if (Violacao([fatoUf]) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(fatoUf));
        }

        FatoUf = fatoUf;
    }

    public override TipoRestricaoValor Tipo => TipoRestricaoValor.MunicipiosDaUf;

    /// <summary>O fato da UF de que o município depende.</summary>
    public string FatoUf { get; }

    public override IReadOnlyCollection<string> FatosCitados => [FatoUf];

    public override Ternario Avaliar(JsonElement resposta, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        if (!fatos.TryGetValue(FatoUf, out FatoResolvido? uf) || uf is null || uf.Estado == EstadoFato.Indeterminado)
        {
            return Ternario.Indeterminado;
        }

        return ComoTernario(uf.Estado == EstadoFato.Resolvido
            && uf.Valor!.Value.ValueKind == JsonValueKind.String
            && resposta.ValueKind == JsonValueKind.String
            && ReferenciaCidadeGeo.UfDoCodigoIbge(resposta.GetString()!) is { } ufDoMunicipio
            && string.Equals(ufDoMunicipio, uf.Valor.Value.GetString(), StringComparison.Ordinal));
    }

    internal static string? Violacao(IReadOnlyCollection<string> fatos) =>
        fatos.Count != 1 || fatos.Any(string.IsNullOrWhiteSpace) ? "Os municípios da UF citam exatamente um fato, o da UF." : null;
}
