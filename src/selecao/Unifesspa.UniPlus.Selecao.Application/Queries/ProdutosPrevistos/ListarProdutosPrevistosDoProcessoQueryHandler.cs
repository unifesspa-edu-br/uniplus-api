namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lista os itens que a versão vigente do edital declara publicar, e diz de cada um se está aberto ou publicado.
/// </summary>
/// <remarks>
/// As declarações vêm da versão congelada; a situação vem do registro do ato do processo, que
/// reconhece o item pela identidade estável dele, e não pelo <c>Id</c> do produto na versão. A configuração em rascunho não entra: uma
/// retificação em curso só passa a valer quando publicada.
/// </remarks>
public sealed class ListarProdutosPrevistosDoProcessoQueryHandler(
    GrafoVigenteDoProcesso grafos,
    IAtoDoProcessoRepository atos,
    IDocumentoAPublicarRepository documentos,
    ITipoAtoPublicadoReader tiposDeAto,
    IResolvedorFusoInstitucional fusos,
    TimeProvider relogio)
{
    public async Task<Result<ProdutosPrevistosDoProcessoDto>> Handle(
        ListarProdutosPrevistosDoProcessoQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        DateTimeOffset agora = relogio.GetUtcNow();
        Result<GrafoVigente> vigente = await grafos.LerAsync(query.ProcessoSeletivoId, agora, cancellationToken).ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return Result<ProdutosPrevistosDoProcessoDto>.Failure(vigente.Error!);
        }
        Result<TimeZoneInfo> fuso = fusos.Resolver();
        return fuso.IsFailure
            ? Result<ProdutosPrevistosDoProcessoDto>.Failure(fuso.Error!)
            : await ProjetarAsync(query.ProcessoSeletivoId, vigente.Value!, fuso.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ProdutosPrevistosDoProcessoDto>> ProjetarAsync(
        Guid processoId, GrafoVigente vigente, TimeZoneInfo fuso, CancellationToken cancellationToken)
    {
        IReadOnlyList<AtoDoProcesso> publicados = await atos.ListarDoProcessoAsync(processoId, cancellationToken).ConfigureAwait(false);
        CadeiasDosProdutosPrevistos cadeias = CadeiasDosProdutosPrevistos.De(publicados);
        Dictionary<Guid, DocumentoAPublicar> documentosPorId = await LerDocumentosAsync(processoId, vigente.Grafo, cadeias, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TipoAtoPublicadoView> tipos = await tiposDeAto.ListarVigentesAsync(
            ProjecaoDosProdutosPrevistos.TiposDeAtoDeclarados(vigente.Grafo),
            ResolucaoDosTiposDeAtoUnicos.Hoje(relogio, fuso), cancellationToken).ConfigureAwait(false);
        return Result<ProdutosPrevistosDoProcessoDto>.Success(ProjecaoDosProdutosPrevistos.Projetar(
            vigente.NumeroDaVersao, vigente.Grafo, cadeias,
            tipos.GroupBy(static t => t.Codigo, StringComparer.Ordinal)
                .ToDictionary(static g => g.Key, static g => g.First().Nome, StringComparer.Ordinal),
            ato => AtoDoProcessoMapping.ToDto(ato, documentosPorId)));
    }

    private async Task<Dictionary<Guid, DocumentoAPublicar>> LerDocumentosAsync(
        Guid processoId, Domain.ValueObjects.GrafoConfiguracao grafo, CadeiasDosProdutosPrevistos cadeias, CancellationToken cancellationToken)
    {
        AtoDoProcesso[] atosDosProdutos = [.. cadeias.AtosDe(ProjecaoDosProdutosPrevistos.ItensDeclarados(grafo))];
        Guid[] ids = [.. atosDosProdutos
            .SelectMany(static a => a.Anexos.Select(static x => x.DocumentoAPublicarId).Append(a.DocumentoPrincipalId)).Distinct()];
        IReadOnlyList<DocumentoAPublicar> encontrados = ids.Length == 0
            ? []
            : await documentos.ListarDoProcessoAsync(processoId, ids, cancellationToken).ConfigureAwait(false);
        return encontrados.ToDictionary(static d => d.Id);
    }
}
