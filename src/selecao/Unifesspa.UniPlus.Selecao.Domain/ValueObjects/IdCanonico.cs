namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A identidade <b>canônica</b> de um nó do grafo de dependência para fins de congelamento
/// (Story #928, §7.1): <c>tipoDeNo/escopo/codigo</c>, em que o escopo é o processo seletivo que
/// contém o nó.
/// </summary>
/// <remarks>
/// <para>
/// A identidade de <b>runtime</b> de um nó (<see cref="NoGrafoDependencia"/>) é o par
/// <c>(Classe, Codigo)</c> — basta para a detecção de ciclo e a ordem <b>dentro de um
/// processo</b>. A identidade <b>canônica</b> acrescenta o escopo: o mesmo código de fato em
/// dois processos distintos são nós distintos, e o hash RN08 não pode confundi-los. Campo e
/// fato do mesmo código continuam distintos pelo <c>tipoDeNo</c>.
/// </para>
/// <para>
/// A gramática é <b>fechada</b> e o <c>/</c> é reservado: nenhum componente pode contê-lo, sob
/// pena de a identidade de dois nós diferentes colidir por remontagem ambígua. Os componentes
/// são ASCII estrutural — os códigos do vocabulário são <c>UPPER_SNAKE</c> e as identidades de
/// exigência são GUID em hex, ambos subconjuntos de ASCII imprimível. A ordenação é por
/// <b>bytes UTF-8</b>, não por <see cref="StringComparer.Ordinal"/>: coincidem enquanto tudo é
/// ASCII, mas a chave total do envelope depende da ordem de bytes, e é essa que se declara.
/// </para>
/// </remarks>
public sealed class IdCanonico : IComparable<IdCanonico>, IEquatable<IdCanonico>
{
    private const char Delimitador = '/';
    private const string LiteralEscopoProcesso = "PROCESSO";

    private readonly byte[] _bytes;

    private IdCanonico(string valor, byte[] bytes)
    {
        Valor = valor;
        _bytes = bytes;
    }

    /// <summary>A forma textual canônica — <c>tipoDeNo/PROCESSO/&lt;id&gt;/codigo</c>.</summary>
    public string Valor { get; }

    /// <summary>Os bytes UTF-8 de <see cref="Valor"/>, a base da ordenação e da igualdade.</summary>
    public ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>
    /// Compõe a identidade canônica de um nó do grafo de um processo. <paramref name="codigo"/>
    /// é o código do fato (campo/fato) ou a identidade da exigência (GUID em hex) — já validado
    /// a montante; aqui só se garante a gramática da identidade.
    /// </summary>
    public static IdCanonico De(ClasseNoGrafo classe, Guid processoId, string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string tipoDeNo = TipoDeNo(classe);
        string codigoCanonico = codigo.Normalize(NormalizationForm.FormC);

        ExigirComponente(tipoDeNo, nameof(classe));
        ExigirComponente(codigoCanonico, nameof(codigo));

        string valor = string.Join(
            Delimitador,
            tipoDeNo,
            LiteralEscopoProcesso,
            processoId.ToString("N", CultureInfo.InvariantCulture),
            codigoCanonico);

        return new IdCanonico(valor, Encoding.UTF8.GetBytes(valor));
    }

    /// <summary>O token de <c>tipoDeNo</c> de cada classe — parte da gramática congelada.</summary>
    private static string TipoDeNo(ClasseNoGrafo classe) => classe switch
    {
        ClasseNoGrafo.Campo => "CAMPO",
        ClasseNoGrafo.Fato => "FATO",
        ClasseNoGrafo.Exigencia => "EXIGENCIA",
        _ => throw new ArgumentOutOfRangeException(nameof(classe), classe, "Classe de nó do grafo desconhecida."),
    };

    private static void ExigirComponente(string componente, string nomeParametro)
    {
        foreach (char c in componente)
        {
            if (c is < '!' or > '~' || c == Delimitador)
            {
                throw new ArgumentException(
                    $"O componente '{componente}' da identidade canônica só admite ASCII imprimível sem o delimitador " +
                    $"'{Delimitador}' — encontrado o caractere U+{(int)c:X4}.",
                    nomeParametro);
            }
        }
    }

    public int CompareTo(IdCanonico? other) =>
        other is null ? 1 : _bytes.AsSpan().SequenceCompareTo(other._bytes);

    public bool Equals(IdCanonico? other) =>
        other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => obj is IdCanonico outro && Equals(outro);

    public override int GetHashCode() => string.GetHashCode(Valor, StringComparison.Ordinal);

    public override string ToString() => Valor;

    public static bool operator ==(IdCanonico? esquerda, IdCanonico? direita) =>
        esquerda is null ? direita is null : esquerda.Equals(direita);

    public static bool operator !=(IdCanonico? esquerda, IdCanonico? direita) => !(esquerda == direita);

    public static bool operator <(IdCanonico? esquerda, IdCanonico? direita) =>
        Comparar(esquerda, direita) < 0;

    public static bool operator <=(IdCanonico? esquerda, IdCanonico? direita) =>
        Comparar(esquerda, direita) <= 0;

    public static bool operator >(IdCanonico? esquerda, IdCanonico? direita) =>
        Comparar(esquerda, direita) > 0;

    public static bool operator >=(IdCanonico? esquerda, IdCanonico? direita) =>
        Comparar(esquerda, direita) >= 0;

    private static int Comparar(IdCanonico? esquerda, IdCanonico? direita) =>
        esquerda is null ? (direita is null ? 0 : -1) : esquerda.CompareTo(direita);
}
