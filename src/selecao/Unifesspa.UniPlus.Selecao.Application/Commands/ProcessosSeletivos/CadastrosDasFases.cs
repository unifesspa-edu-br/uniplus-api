namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Os cadastros que a edição do cronograma consulta. A unidade responsável e a categoria de documento são
/// lidas uma vez por chave, porque um cronograma real repete a mesma categoria em bancas de fases
/// diferentes; a fase canônica, o tipo de banca e a regra são lidos a cada uso. O catálogo de fatos, de que o
/// enquadramento da regra de homologação depende, é lido uma vez por edição.
/// </summary>
internal sealed class CadastrosDasFases(
    IFaseCanonicaReader faseCanonicaReader,
    IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
    ITipoBancaReader tipoBancaReader,
    ICategoriaDocumentoReader categoriaDocumentoReader,
    IRegraCatalogoReader regraCatalogoReader,
    IFatoCandidatoReader fatoCandidatoReader)
{
    public IFaseCanonicaReader FasesCanonicas { get; } = faseCanonicaReader;

    public ITipoBancaReader TiposDeBanca { get; } = tipoBancaReader;

    public IRegraCatalogoReader Regras { get; } = regraCatalogoReader;

    public LeituraMemoizada<Guid, UnidadeResponsavelSnapshot> Unidades { get; } =
        new((unidadeId, cancellationToken) => unidadeResponsavelResolvedor.ResolverAsync(unidadeId, cancellationToken));

    public LeituraMemoizada<Guid, CategoriaDocumentoView> Categorias { get; } =
        new((categoriaId, cancellationToken) => categoriaDocumentoReader.ObterPorIdAsync(categoriaId, cancellationToken));

    public LeituraMemoizada<ProcessoSeletivo, ContextoDoCatalogo> Catalogos { get; } =
        new(async (processo, cancellationToken) => ContextoDoCatalogo.De(
            processo, await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false)));
}
