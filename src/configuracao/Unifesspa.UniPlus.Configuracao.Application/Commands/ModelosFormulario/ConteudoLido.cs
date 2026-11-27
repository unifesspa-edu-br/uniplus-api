namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O conteúdo do modelo lido da escrita, com a posição de cada parte na entrada: as recusas da
/// conferência contra o catálogo apontam o índice que veio. A parte que não se leu fica de fora; a
/// recusa dela está em <see cref="LeituraDoConteudo.Recusas"/>.
/// </summary>
/// <param name="PorInteiro">Se a leitura não recusou nada: o conteúdo lido é o enviado, sem parte de fora.</param>
internal sealed record ConteudoLido(
    string? Titulo,
    IReadOnlyList<(int Indice, PaginaDoModelo Pagina)> Paginas,
    IReadOnlyList<(int Indice, ItemDoModelo Item)> Itens,
    IReadOnlyList<(int Indice, TermoDoModelo Termo)> Termos,
    IReadOnlyList<string> Pressupostos,
    GruposLidos Grupos,
    ConjuntoLidoDeDocumentos Documentos,
    bool PorInteiro)
{
    public ConteudoDoModelo ParaConteudo() => new(
        Titulo, [.. Paginas.Select(static e => e.Pagina)], [.. Itens.Select(static i => i.Item)], [.. Termos.Select(static t => t.Termo)], Pressupostos,
        [.. Grupos.Lidos.Select(static g => g.Grupo)], Documentos.ParaConjunto());

    /// <summary>
    /// As recusas do domínio contam as partes lidas; a parte que a leitura deixou de fora desloca as seguintes.
    /// Aqui cada recusa volta à posição que veio na entrada, e a que não tem posição fica como veio.
    /// </summary>
    public IEnumerable<FieldError> NasPosicoesDaEntrada(IEnumerable<FieldError> doDominio)
    {
        ArgumentNullException.ThrowIfNull(doDominio);
        ListaDaEntrada[] listas =
        [
            Lista("paginas", Paginas.Select(static e => e.Indice)),
            Lista("itens", Itens.Select(static e => e.Indice)),
            Lista("termos", Termos.Select(static e => e.Indice)),
            Lista("grupos", Grupos.Lidos.Select(static e => e.Indice)),
            Lista(ConjuntoDeDocumentosDoModelo.ListaDosDocumentos, Documentos.DocumentosExigidos.Select(static e => e.Indice)),
            Lista(ConjuntoDeDocumentosDoModelo.ListaDosGrupos, Documentos.GruposDeDocumentos.Select(static e => e.Indice)),
        ];
        return doDominio.Select(erro => erro.Field is { } campo ? erro with { Field = NaPosicaoDaEntrada(campo, listas) } : erro);
    }

    private static ListaDaEntrada Lista(string caminho, IEnumerable<int> posicoes) =>
        new($"{ConferenciaDoConteudoDoModelo.CaminhoDoConteudo}.{caminho}", [.. posicoes]);

    private static string NaPosicaoDaEntrada(string campo, ListaDaEntrada[] listas) =>
        listas.Select(l => l.NaPosicaoDaEntrada(campo)).FirstOrDefault(static c => c is not null) ?? campo;
}

/// <summary>
/// Os grupos que a leitura aceitou, com a posição de cada um na entrada, e os campos que ela aceitou nos grupos que
/// recusou: esses seguem só para a conferência no catálogo, e não fazem parte do conteúdo.
/// </summary>
internal sealed record GruposLidos(
    IReadOnlyList<(int Indice, GrupoDoModelo Grupo)> Lidos, IReadOnlyList<CampoDeGrupoNaoLido> CamposDeGruposNaoLidos);

/// <summary>Um campo lido de um grupo que a leitura recusou, com a posição do grupo e a do campo na entrada.</summary>
internal sealed record CampoDeGrupoNaoLido(int Grupo, int Posicao, ItemDoModelo Campo)
{
    public string Caminho => $"grupos[{Grupo}].subitens[{Posicao}]";
}
