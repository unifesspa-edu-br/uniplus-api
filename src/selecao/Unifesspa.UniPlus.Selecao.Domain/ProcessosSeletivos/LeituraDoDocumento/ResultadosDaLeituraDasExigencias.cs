namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// A exigência que a leitura adotou, ou <see langword="null"/> quando a recusou ou a cortou, com a posição dela no documento, a chave
/// e as recusas, com o campo relativo à exigência.
/// </summary>
internal sealed record ExigenciaLida(DocumentoExigido? Documento, int PosicaoNoDocumento, string? Chave, IReadOnlyList<FieldError> Recusas);

/// <summary>
/// As exigências que a leitura adotou, cada uma com a posição em que veio, o documento exigido de cada chave adotada, as chaves das
/// que ela recusou, que as regras de comprovação consultam para achar a exigência que citam, e as partes recusadas. A chave recusada
/// se distingue da inexistente: quem cita a recusada não acumula causa nova.
/// </summary>
internal sealed record DocumentosLidos(
    IReadOnlyList<(DocumentoExigido Documento, int PosicaoNoDocumento)> Adotados,
    IReadOnlyDictionary<string, DocumentoExigido> PelaChave,
    IReadOnlySet<string> ChavesRecusadas,
    IReadOnlyList<ParteRecusada> Recusadas);

/// <summary>
/// A regra de comprovação que a leitura adotou, ou <see langword="null"/> quando a recusou ou a cortou, com a posição dela na lista
/// em que veio, os itens lidos, adotados ou não, e as recusas dela e dos itens, com o campo já apontando o documento.
/// </summary>
internal sealed record NoLido(RegraDeComprovacao? Regra, int Posicao, IReadOnlyList<NoLido> Itens, IReadOnlyList<FieldError> Recusas);

/// <summary>A regra de comprovação que a leitura montou sem os itens, ou <see langword="null"/>, e as recusas dela, com o campo relativo à regra.</summary>
internal sealed record RegraPropriaLida(RegraDeComprovacao? Regra, IReadOnlyList<FieldError> Recusas);

/// <summary>As regras de comprovação lidas, com os nós da árvore na ordem enviada.</summary>
internal sealed record ArvoreLida(IReadOnlyList<NoLido> Raizes)
{
    /// <summary>As regras adotadas, cada uma seguida dos itens dela, como o processo as guarda.</summary>
    internal IReadOnlyList<RegraDeComprovacao> Regras =>
        [.. Raizes.Where(static raiz => raiz.Regra is not null).SelectMany(static raiz => raiz.Regra!.EnumerarRegraEItens())];

    internal IReadOnlyList<ParteRecusada> Recusadas =>
        [.. Raizes.SelectMany(static raiz => raiz.Recusas.Select(recusa => PartesRecusadasDasExigencias.DaRegra(raiz.Posicao, recusa)))];
}
