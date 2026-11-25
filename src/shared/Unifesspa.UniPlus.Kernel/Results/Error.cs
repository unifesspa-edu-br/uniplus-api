namespace Unifesspa.UniPlus.Kernel.Results;

public sealed record DomainError(string Code, string Message);

/// <summary>
/// O identificador estável de um item de lista que o campo de um <see cref="FieldError"/> atravessa: o nome da lista e o
/// identificador do item, que é um código de catálogo ou um identificador opaco, nunca dado pessoal.
/// </summary>
public sealed record IdentificadorDoItem(string Lista, string Identificador);

/// <summary>
/// Um <see cref="DomainError"/> associado ao campo de origem — usado quando um
/// <see cref="Result"/> agrega mais de uma violação (ex.: várias regras de
/// <c>Entidade.ValidarCampos</c> falhando no mesmo lote). <see cref="Field"/> é
/// <see langword="null"/> para erros que não são de campo específico (ex.:
/// "referenciado por registro vivo").
/// <para>
/// <see cref="Origem"/> identifica de dentro o que causou a recusa (a regra e o fato citado, por
/// exemplo) quando o campo e o código não bastam para distinguir duas recusas; não vai para o wire.
/// </para>
/// <para>
/// <see cref="Identificadores"/> diz, para cada lista que o campo percorre, qual item é o da causa, pelo identificador
/// estável dele, de modo que o cliente o ache mesmo depois de outra gravação mudar as posições; vai para o wire.
/// </para>
/// </summary>
public sealed record FieldError(
    string? Field, DomainError Error, string? Origem = null, IReadOnlyList<IdentificadorDoItem>? Identificadores = null)
{
    /// <summary>
    /// A causa dentro de um contêiner. A causa sem campo (<see langword="null"/>) continua sem campo; a raiz
    /// relativa (<c>""</c>) passa a ser o caminho do contêiner; um campo interno vira filho dele, com índices
    /// entre colchetes unidos sem ponto. O caminho vazio representa a raiz do documento e deixa a causa como está.
    /// </summary>
    public FieldError NoCaminhoDe(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);

        return (caminho, Field) switch
        {
            ("", _) or (_, null) => this,
            (_, { Length: 0 }) => this with { Field = caminho },
            (_, ['[', ..]) => this with { Field = $"{caminho}{Field}" },
            _ => this with { Field = $"{caminho}.{Field}" },
        };
    }
}
