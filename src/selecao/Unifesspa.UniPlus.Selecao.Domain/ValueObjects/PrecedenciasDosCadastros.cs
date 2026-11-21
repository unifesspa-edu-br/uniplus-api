namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A precedência entre fases que a aplicação leu do cadastro, uma vez por operação, ou o registro de que não leu. Quem não leu
/// não pede a conferência da precedência, e isso é diferente de a leitura trazer lista vazia: a lista vazia diz que o cadastro
/// não tem relação nenhuma, e a conferência a julga.
/// </summary>
public sealed class PrecedenciasDosCadastros
{
    private readonly IReadOnlyList<ArestaPrecedencia>? _arestas;

    private PrecedenciasDosCadastros(IReadOnlyList<ArestaPrecedencia>? arestas) => _arestas = arestas;

    /// <summary>A operação não pede a conferência da precedência: o cronograma dela não mudou desde a versão publicada.</summary>
    public static PrecedenciasDosCadastros NaoLidas { get; } = new(null);

    /// <summary>Se a aplicação leu a precedência do cadastro, e por isso a conferência a julga.</summary>
    public bool FoiLida => _arestas is not null;

    /// <summary>As relações lidas do cadastro; vazia quando a precedência não foi lida.</summary>
    public IReadOnlyList<ArestaPrecedencia> Arestas => _arestas ?? [];

    /// <summary>As relações que o cadastro tinha no momento da leitura.</summary>
    public static PrecedenciasDosCadastros Lidas(IReadOnlyList<ArestaPrecedencia> arestas)
    {
        ArgumentNullException.ThrowIfNull(arestas);

        return new([.. arestas]);
    }
}
