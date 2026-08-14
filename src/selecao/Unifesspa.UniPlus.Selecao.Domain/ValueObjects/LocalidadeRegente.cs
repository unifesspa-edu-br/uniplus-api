namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O município cujo calendário rege a contagem dos prazos do certame (UNI-REQ-0111):
/// é dele que se derivam os feriados municipais e, pelo prefixo do código, os estaduais
/// que incidem sobre as janelas de recurso.
/// </summary>
/// <remarks>
/// <para>Declarada por processo, nunca deduzida da Unidade administradora, do campus de
/// oferta ou da sede: o certame pode correr sob o calendário de outro município, e deduzir
/// devolveria a rigidez que tornou a localidade configurável. A interface pode apresentar
/// o campo já preenchido, mas o valor só existe aqui depois de declarado.</para>
/// <para>O <see cref="CodigoIbge"/> é o <strong>único valor normativo</strong>.
/// <see cref="Nome"/> e <see cref="Uf"/> são cache de exibição, e por isso não participam
/// da igualdade: dois processos que declarem o mesmo código regem-se pelo mesmo calendário
/// ainda que um deles exiba o município com nome divergente — rótulo errado, prazo certo.
/// É o que obriga a sobrescrever a igualdade estrutural que <c>record</c> daria de graça.</para>
/// </remarks>
public sealed record LocalidadeRegente
{
    private LocalidadeRegente(string codigoIbge, string nome, string uf)
    {
        CodigoIbge = codigoIbge;
        Nome = nome;
        Uf = uf;
    }

    /// <summary>Código IBGE do município, de sete dígitos — o valor normativo.</summary>
    public string CodigoIbge { get; }

    /// <summary>Nome do município, cache de exibição.</summary>
    public string Nome { get; }

    /// <summary>Sigla da UF, cache de exibição — o prefixo do código é quem determina a real.</summary>
    public string Uf { get; }

    /// <summary>
    /// Valida a forma e a coerência do trio pelo mesmo caminho que <c>Campus</c>,
    /// <c>LocalOferta</c> e o snapshot da Unidade administradora já usam, sem duplicar as
    /// causas que o Kernel nomeia. Ao contrário daquele snapshot, aqui o trio não é
    /// opcional: a localidade é exigida desde a criação do processo.
    /// </summary>
    public static Result<LocalidadeRegente> Criar(string? codigoIbge, string? nome, string? uf)
    {
        Result validacao = ReferenciaCidadeGeo.Validar(codigoIbge, nome, uf);
        if (validacao.IsFailure)
        {
            return Result<LocalidadeRegente>.Failure(validacao.Error!);
        }

#pragma warning disable CA1062 // Validar acabou de provar os três não-nulos; o analisador não relaciona a prova entre parâmetros distintos (mesmo caso de UnidadeAdministradoraSnapshot).
        return Result<LocalidadeRegente>.Success(Normalizar(codigoIbge, nome, uf));
#pragma warning restore CA1062
    }

    private static LocalidadeRegente Normalizar(string? codigoIbge, string? nome, string? uf) =>
        new(codigoIbge!.Trim(), nome!.Trim(), uf!.Trim().ToUpperInvariant());

    /// <summary>
    /// Igualdade pelo código apenas — ver o porquê na documentação do tipo. Corrigir o
    /// cache de exibição não muda a localidade que rege a contagem, e por isso não pode
    /// produzir duas localidades diferentes.
    /// </summary>
    public bool Equals(LocalidadeRegente? other) =>
        other is not null && string.Equals(CodigoIbge, other.CodigoIbge, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(CodigoIbge);
}
