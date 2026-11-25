namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O conteúdo do modelo lido da escrita, com a posição de cada parte na entrada: as recusas da
/// conferência contra o catálogo apontam o índice que veio. A parte que não se leu fica de fora; a
/// recusa dela está em <see cref="LeituraDoConteudo.Recusas"/>.
/// </summary>
internal sealed record ConteudoLido(
    string? Titulo,
    IReadOnlyList<(int Indice, PaginaDoModelo Pagina)> Paginas,
    IReadOnlyList<(int Indice, ItemDoModelo Item)> Itens,
    IReadOnlyList<(int Indice, TermoDoModelo Termo)> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<(int Indice, GrupoDoModelo Grupo)> Grupos,
    ConjuntoLidoDeDocumentos Documentos)
{
    public ConteudoDoModelo ParaConteudo() => new(
        Titulo, [.. Paginas.Select(static e => e.Pagina)], [.. Itens.Select(static i => i.Item)], [.. Termos.Select(static t => t.Termo)], Pressupostos,
        [.. Grupos.Select(static g => g.Grupo)], Documentos.ParaConjunto());
}
