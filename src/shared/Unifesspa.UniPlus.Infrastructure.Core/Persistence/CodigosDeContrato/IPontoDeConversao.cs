namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Lugar do modelo que guarda um valor e aceita um conversor: a propriedade escalar ou o elemento de uma coleção
/// primitiva, com acesso ao conversor e ao comentário da coluna correspondente.
/// </summary>
internal interface IPontoDeConversao
{
    /// <summary>O tipo do valor como o modelo o declara, inclusive o <see cref="Nullable{T}"/>.</summary>
    Type TipoDeclarado { get; }

    /// <summary>Onde o ponto está no modelo, em frase iniciada por artigo, para a mensagem do defeito.</summary>
    string Descricao { get; }

    /// <summary>O conversor configurado, por instância ou por tipo, ou nulo quando não há.</summary>
    ValueConverter? Conversor { get; }

    /// <summary>
    /// O tipo no provedor, que <c>HasConversion&lt;string&gt;()</c> define sem instância de conversor, ou nulo quando
    /// não há.
    /// </summary>
    Type? TipoNoProvedor { get; }

    /// <summary>O comentário da coluna, quando definido.</summary>
    string? Comentario { get; }

    void DefinirConversor(ValueConverter conversor);

    void DefinirComentario(string comentario);
}
