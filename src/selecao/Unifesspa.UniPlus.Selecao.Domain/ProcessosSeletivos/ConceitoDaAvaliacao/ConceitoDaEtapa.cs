namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O conceito com que a etapa é avaliada, copiado por valor do catálogo de tipos de conceito no momento em
/// que a etapa o declara (ADR-0061, ADR-0169): código, nome, natureza e a escala ou os valores categóricos.
/// A etapa nunca relê o catálogo para mudar a própria cópia; o conceito alterado ou desativado depois
/// não alcança o que já foi declarado.
/// </summary>
/// <remarks>
/// As guardas repetem as do catálogo: a cópia também é reposta de um envelope, que pode ter sido adulterado.
/// Os campos das recusas são relativos ao conceito.
/// </remarks>
public sealed record ConceitoDaEtapa
{
    private const int CodigoMaximo = FormatoDoCodigo.ComprimentoMaximo;
    private const int NomeMaximo = 200;

    private ConceitoDaEtapa(string codigo, string nome, ParametrosDoConceito parametros)
    {
        Codigo = codigo;
        Nome = nome;
        Natureza = parametros.Natureza;
        Escala = parametros.Escala;
        Valores = parametros.Valores;
    }

    public string Codigo { get; }
    public string Nome { get; }
    public FormaDoConceito Natureza { get; }

    /// <summary>A escala do conceito numérico; nula no categórico.</summary>
    public EscalaNumerica? Escala { get; }

    /// <summary>Os valores do conceito categórico, na ordem do cadastro; vazia no numérico.</summary>
    public IReadOnlyList<ValorCategorico> Valores { get; }

    /// <summary>
    /// Confere a cópia e acumula as recusas (ADR-0125). <paramref name="natureza"/> já é o valor do enum:
    /// quem lê o código do catálogo de tipos de conceito ou do conteúdo congelado o converte antes, com a recusa do seu canal.
    /// </summary>
    public static Result<ConceitoDaEtapa> Criar(
        string? codigo,
        string? nome,
        FormaDoConceito natureza,
        EscalaInformada? escala,
        IReadOnlyList<ValorCategoricoInformado?>? valores)
    {
        List<FieldError> erros = [];
        string? codigoCopiado = TextoDoConceito.Conferir(
            codigo, CodigoMaximo, "codigo", ConceitoDaEtapaErrorCodes.CodigoInvalido, "O código do conceito", erros);
        string? nomeCopiado = TextoDoConceito.Conferir(
            nome, NomeMaximo, "nome", ConceitoDaEtapaErrorCodes.NomeInvalido, "O nome do conceito", erros);
        ParametrosDoConceito? parametros = ParametrosDoConceito.Conferir(natureza, escala, valores, erros);

        return erros.Count > 0
            ? Result<ConceitoDaEtapa>.ValidationFailure(erros)
            : Result<ConceitoDaEtapa>.Success(new(codigoCopiado!, nomeCopiado!, parametros!));
    }

    /// <summary>O valor de código <paramref name="codigo"/>, ou <see langword="null"/> se o conceito não o tem (inclusive o numérico).</summary>
    public ValorCategorico? ValorDeCodigo(string codigo) =>
        Valores.FirstOrDefault(valor => string.Equals(valor.Codigo, codigo, StringComparison.Ordinal));

    /// <summary>Se o valor conta como aprovação. O valor que o conceito não tem não conta.</summary>
    public bool ContaComoAprovacao(string codigo) => ValorDeCodigo(codigo)?.ContaComoAprovacao ?? false;

    /// <summary>Os conceitos são iguais quando têm os mesmos dados e os mesmos valores na mesma ordem.</summary>
    public bool Equals(ConceitoDaEtapa? outro) =>
        outro is not null
        && Codigo == outro.Codigo
        && Nome == outro.Nome
        && Natureza == outro.Natureza
        && Equals(Escala, outro.Escala)
        && Valores.SequenceEqual(outro.Valores);

    public override int GetHashCode()
    {
        HashCode codigo = new();
        codigo.Add(Codigo);
        codigo.Add(Nome);
        codigo.Add(Natureza);
        codigo.Add(Escala);
        foreach (ValorCategorico valor in Valores)
        {
            codigo.Add(valor);
        }

        return codigo.ToHashCode();
    }
}
