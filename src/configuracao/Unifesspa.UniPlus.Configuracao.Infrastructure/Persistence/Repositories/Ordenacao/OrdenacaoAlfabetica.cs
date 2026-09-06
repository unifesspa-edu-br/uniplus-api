namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Linha de leitura de Curso ordenada alfabeticamente: a entidade acompanhada
/// das columns que decidem a posição antes do <c>Id</c>.
/// </summary>
/// <remarks>
/// O motor de keyset ordena por propriedades do tipo que ele pagina, e a chave
/// de ordenação do curso é propriedade sombra — invisível na entidade
/// materializada. Projetar para este carrier resolve isso sem levar a coluna de
/// ordenação para dentro do domínio, e ainda serve de âncora de continuação: a
/// referência que o motor consome é uma instância dele com as colunas
/// preenchidas.
/// </remarks>
internal sealed class CursoOrdenado : IIdentificavel
{
    public Guid Id { get; init; }

    public string NomeOrdenacao { get; init; } = string.Empty;

    public string Codigo { get; init; } = string.Empty;

    public Curso Entidade { get; init; } = null!;
}

/// <summary>
/// Linha de leitura de Oferta de Curso ordenada pelo curso associado: a oferta
/// acompanhada da chave de ordenação e do código <b>do curso</b>.
/// </summary>
/// <remarks>
/// A oferta não navega para o curso — a chave estrangeira é declarada sem
/// propriedade de navegação. A ordenação pelo nome do curso vem então de uma
/// junção explícita projetada neste carrier, em vez de acrescentar navegação à
/// entidade só para servir a listagem.
/// </remarks>
internal sealed class OfertaCursoOrdenada : IIdentificavel
{
    public Guid Id { get; init; }

    public string NomeOrdenacao { get; init; } = string.Empty;

    public string Codigo { get; init; } = string.Empty;

    public OfertaCurso Entidade { get; init; } = null!;
}

/// <summary>
/// Ordenação padrão das listagens de Curso e de Oferta de Curso: nome do curso
/// em ordem alfabética, código do curso como desempate, e o <c>Id</c> — que o
/// motor acrescenta — como critério final.
/// </summary>
/// <remarks>
/// <para>Os dois carriers expõem as mesmas colunas com os mesmos nomes, então a
/// ordenação é declarada uma vez para os dois: a convenção de comparação é
/// literalmente a mesma nas duas rotas, não duas cópias que podem divergir.</para>
/// <para>Escolher colunas e sentido em tempo de execução é montar uma
/// <see cref="KeysetSort{T}"/> diferente com as mesmas peças; o padrão aqui
/// é o que vale quando a consulta não pede ordenação alguma.</para>
/// </remarks>
internal static class OrdenacaoAlfabeticaDoCurso
{
    /// <summary>Nome público da coluna de nome do curso.</summary>
    internal const string TokenNome = "nome";

    /// <summary>Nome público da coluna de código do curso.</summary>
    internal const string TokenCodigo = "codigo";

    public static KeysetSort<CursoOrdenado> Cursos { get; } = new(
        [
            KeysetSortColumn<CursoOrdenado>.For(
                TokenNome, c => c.NomeOrdenacao, c => c.NomeOrdenacao),
            KeysetSortColumn<CursoOrdenado>.For(
                TokenCodigo, c => c.Codigo, c => c.Codigo),
        ],
        (partes, id) => new CursoOrdenado
        {
            NomeOrdenacao = partes[0],
            Codigo = partes[1],
            Id = id,
        });

    public static KeysetSort<OfertaCursoOrdenada> OfertasCurso { get; } = new(
        [
            KeysetSortColumn<OfertaCursoOrdenada>.For(
                TokenNome, o => o.NomeOrdenacao, o => o.NomeOrdenacao),
            KeysetSortColumn<OfertaCursoOrdenada>.For(
                TokenCodigo, o => o.Codigo, o => o.Codigo),
        ],
        (partes, id) => new OfertaCursoOrdenada
        {
            NomeOrdenacao = partes[0],
            Codigo = partes[1],
            Id = id,
        });
}
