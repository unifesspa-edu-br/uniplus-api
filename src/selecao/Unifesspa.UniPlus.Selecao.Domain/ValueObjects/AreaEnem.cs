namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Collections.ObjectModel;
using System.Text;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Área do ENEM — texto de domínio <b>fechado</b> das cinco notas que o INEP
/// divulga por candidato: as quatro áreas de conhecimento e a redação. É o
/// vocabulário que permite a uma regra citar uma área pelo valor, com domínio
/// validável, em vez de depender das cinco colunas nomeadas do cadastro de
/// pesos por grupo de curso.
/// </summary>
/// <remarks>
/// Mesma forma de <c>GrupoCurso</c>: rótulo de referência (com acento e
/// espaço), não enumerado compilado, comparado por <see cref="StringComparer.Ordinal"/>
/// depois de recomposto em NFC. A grafia repete os nomes com que o cadastro de
/// pesos por grupo de curso descreve as suas cinco colunas de peso; lá elas são
/// colunas nomeadas, não valores deste vocabulário, e nada no código amarra uma
/// à outra.
/// </remarks>
public sealed record AreaEnem
{
    /// <summary>Área "Redação".</summary>
    public const string Redacao = "Redação";

    /// <summary>Área "Ciências da Natureza".</summary>
    public const string CienciasDaNatureza = "Ciências da Natureza";

    /// <summary>Área "Ciências Humanas".</summary>
    public const string CienciasHumanas = "Ciências Humanas";

    /// <summary>Área "Linguagens e Códigos".</summary>
    public const string LinguagensECodigos = "Linguagens e Códigos";

    /// <summary>Área "Matemática".</summary>
    public const string Matematica = "Matemática";

    /// <summary>Conjunto canônico das cinco áreas do ENEM.</summary>
    /// <remarks>
    /// Envolto em <see cref="ReadOnlySet{T}"/> para que nenhum consumidor, nem por cast,
    /// alargue ou encolha o domínio de <see cref="Criar"/> para o processo inteiro. A
    /// ordem de enumeração é a da declaração, que é a da lista na mensagem de falha.
    /// </remarks>
    public static readonly IReadOnlySet<string> Valores = new ReadOnlySet<string>(
        new HashSet<string>(StringComparer.Ordinal)
        {
            Redacao,
            CienciasDaNatureza,
            CienciasHumanas,
            LinguagensECodigos,
            Matematica,
        });

    public string Valor { get; }

    private AreaEnem(string valor) => Valor = valor;

    /// <summary>
    /// Cria uma <see cref="AreaEnem"/> validando o valor contra o conjunto
    /// canônico. Valor fora do domínio (ou nulo/em branco) retorna falha de
    /// domínio com a lista dos valores aceitos. O valor é aparado por
    /// <c>Trim</c> e recomposto em NFC antes da comparação.
    /// </summary>
    public static Result<AreaEnem> Criar(string? valor)
    {
        string? canonico = Canonizar(valor);

        if (canonico is null)
        {
            return Result<AreaEnem>.Failure(new DomainError(
                AreaEnemErrorCodes.ForaDoDominio,
                $"Área do ENEM deve ser uma de: {string.Join(", ", Valores)}."));
        }

        return Result<AreaEnem>.Success(new AreaEnem(canonico));
    }

    /// <summary>
    /// Indica se <paramref name="valor"/> pertence ao domínio fechado, sem
    /// construir o value object nem a mensagem de falha.
    /// </summary>
    public static bool EhValido(string? valor) => Canonizar(valor) is not null;

    public override string ToString() => Valor;

    /// <summary>
    /// A grafia canônica que <paramref name="valor"/> representa, ou
    /// <see langword="null"/> se ele estiver fora do domínio.
    /// </summary>
    /// <remarks>
    /// A recomposição em NFC é a mesma que o payload canônico aplica a toda
    /// string de negócio (ADR-0100): sem ela, um acento digitado em forma
    /// decomposta — "Matema" seguido do acento combinante e de "tica" — seria
    /// recusado, embora designe a mesma área que a forma composta listada na
    /// mensagem de falha.
    /// </remarks>
    private static string? Canonizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string composto;
        try
        {
            composto = valor.Trim().Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // Texto com surrogate isolado não tem forma normalizada — e não é
            // área alguma. Recusar como fora do domínio mantém a criação livre
            // de exceção.
            return null;
        }

        return Valores.Contains(composto) ? composto : null;
    }
}
