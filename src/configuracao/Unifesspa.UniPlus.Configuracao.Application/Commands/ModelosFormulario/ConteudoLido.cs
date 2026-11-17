namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O conteúdo do modelo lido da escrita, com a posição de cada parte na entrada: as recusas da
/// leitura e as da conferência contra o catálogo apontam o índice que veio. A parte que não se leu
/// fica de fora, com a recusa dela.
/// </summary>
internal sealed record ConteudoLido(
    string? Titulo,
    IReadOnlyList<(int Indice, EtapaDoModelo Etapa)> Etapas,
    IReadOnlyList<(int Indice, ItemDoModelo Item)> Itens,
    IReadOnlyList<(int Indice, TermoDoModelo Termo)> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<(int Indice, GrupoDoModelo Grupo)> Grupos,
    IReadOnlyList<(int Indice, DocumentoExigidoDoModelo Documento)> Documentos,
    IReadOnlyList<FieldError> Erros)
{
    public ConteudoDoModelo ParaConteudo() => new(
        Titulo, [.. Etapas.Select(static e => e.Etapa)], [.. Itens.Select(static i => i.Item)], [.. Termos.Select(static t => t.Termo)], Pressupostos,
        [.. Grupos.Select(static g => g.Grupo)], [.. Documentos.Select(static d => d.Documento)]);

    /// <summary>
    /// Lê a entrada pela forma das regras do formulário (<see cref="Unifesspa.UniPlus.Regras.Entradas.EntradaDeRegras"/>): token
    /// desconhecido vira o sentinela, que a forma recusa depois; predicado, obrigatoriedade e
    /// restrição malformados são recusados aqui. O formato do campo de texto é o do fato no
    /// catálogo.
    /// </summary>
    public static ConteudoLido Ler(ConteudoDoModeloInput? entrada, IReadOnlyDictionary<string, string> formatos)
    {
        ArgumentNullException.ThrowIfNull(formatos);
        return new LeituraDoConteudo(formatos).Ler(entrada);
    }
}
