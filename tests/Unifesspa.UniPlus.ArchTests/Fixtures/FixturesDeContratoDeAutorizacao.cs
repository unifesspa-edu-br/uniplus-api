namespace Unifesspa.UniPlus.ArchTests.Fixtures;

internal sealed record RegistroMutavel
{
    public string Valor { get; set; } = string.Empty;
}

internal sealed record MotivoComTexto
{
    public string Codigo { get; init; } = string.Empty;

    public string Texto { get; init; } = string.Empty;
}

public sealed record RegistroPublicoMutavel
{
    public string Valor { get; set; } = string.Empty;
}

public record struct RegistroStructMutavel
{
    public int Valor { get; set; }
}

public sealed class ClassePublicaComum;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AtributoPublicoAttribute : Attribute
{
    public string[]? Valores { get; init; }
}

public sealed record RegistroComLista
{
    public List<string> Valores { get; init; } = [];
}

public sealed record RegistroComColecao
{
    public System.Collections.ObjectModel.Collection<string> Valores { get; init; } = [];
}

internal sealed record RegistroComCampo
{
    public readonly List<string> Valores = [];
}

internal sealed record RegistroComCampoArray
{
    public readonly string[] Valores = [];
}

public sealed record RegistroSomenteLeitura
{
    public IReadOnlyList<string> Valores { get; init; } = [];

    public string Texto { get; init; } = string.Empty;
}

public sealed record RegistroComFila
{
    public Queue<string> Valores { get; init; } = new();
}

public sealed record RegistroComDicionario
{
    public Dictionary<string, string> Valores { get; init; } = [];
}

public sealed record RegistroComArray
{
    public string[] Valores { get; init; } = [];
}

public sealed class EnumeravelMutavel<T> : IEnumerable<T>
{
    private readonly List<T> _valores = [];

    public void Adicionar(T valor) => _valores.Add(valor);

    public IEnumerator<T> GetEnumerator() => _valores.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record RegistroComEnumeravelMutavel
{
    public EnumeravelMutavel<string> Valores { get; init; } = new();
}
