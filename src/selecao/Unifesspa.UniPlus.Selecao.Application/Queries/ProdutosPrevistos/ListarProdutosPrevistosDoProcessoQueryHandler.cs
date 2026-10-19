namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lista os itens que a versão vigente do edital declara publicar, com a situação de cada um.
/// </summary>
/// <remarks>
/// As declarações vêm da versão congelada, que é onde o produto tem o <c>Id</c> que o ato cumpre;
/// a situação vem do registro do ato do processo. A configuração em rascunho não entra: uma
/// retificação em curso só passa a valer quando publicada.
/// </remarks>
public static class ListarProdutosPrevistosDoProcessoQueryHandler
{
    public static async Task<Result<ProdutosPrevistosDoProcessoDto>> Handle(
        ListarProdutosPrevistosDoProcessoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ITipoAtoPublicadoReader tipoAtoReader,
        IResolvedorFusoInstitucional resolvedorFuso,
        IOptions<AtosDoProcessoOptions> opcoes,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(tipoAtoReader);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset agora = timeProvider.GetUtcNow();
        Result<GrafoVigente> vigente = await GrafoVigenteDoProcesso
            .LerAsync(processoSeletivoRepository, registroCodecs, query.ProcessoSeletivoId, agora, cancellationToken)
            .ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return Result<ProdutosPrevistosDoProcessoDto>.Failure(vigente.Error!);
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<ProdutosPrevistosDoProcessoDto>.Failure(fuso.Error!);
        }

        Domain.ValueObjects.GrafoConfiguracao grafo = vigente.Value!.Grafo;
        IReadOnlyList<AtoDoProcesso> atos = await atoDoProcessoRepository
            .ListarDoProcessoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        CadeiasDosProdutosPrevistos cadeias = CadeiasDosProdutosPrevistos.De(atos);
        AtoDoProcesso[] atosDosProdutos = [.. cadeias.AtosDe(ProjecaoDosProdutosPrevistos.ProdutosDeclarados(grafo))];

        // Só os documentos dos atos que os itens mostram: o histórico de envios inclui pendentes e
        // documentos que nenhum ato usou.
        Guid[] idsDosDocumentos =
        [
            .. atosDosProdutos
                .SelectMany(static a => a.Anexos.Select(static x => x.DocumentoAPublicarId).Append(a.DocumentoPrincipalId))
                .Distinct(),
        ];
        IReadOnlyList<DocumentoAPublicar> documentos = idsDosDocumentos.Length == 0
            ? []
            : await documentoAPublicarRepository
                .ListarDoProcessoAsync(query.ProcessoSeletivoId, idsDosDocumentos, cancellationToken)
                .ConfigureAwait(false);
        Dictionary<Guid, DocumentoAPublicar> documentosPorId = documentos.ToDictionary(static d => d.Id);

        IReadOnlyList<TipoAtoPublicadoView> tipos = await tipoAtoReader
            .ListarVigentesAsync(
                ProjecaoDosProdutosPrevistos.TiposDeAtoDeclarados(grafo),
                ResolucaoDosTiposDeAtoUnicos.Hoje(timeProvider, fuso.Value),
                cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset limite = agora - opcoes.Value.LimiarDePendencia;
        return Result<ProdutosPrevistosDoProcessoDto>.Success(ProjecaoDosProdutosPrevistos.Projetar(
            vigente.Value.NumeroDaVersao,
            grafo,
            cadeias,
            tipos.GroupBy(static t => t.Codigo, StringComparer.Ordinal)
                .ToDictionary(static g => g.Key, static g => g.First().Nome, StringComparer.Ordinal),
            ato => AtoDoProcessoMapping.ToDto(ato, documentosPorId, AtoDoProcessoMapping.PendenciaAcimaDoLimiar(ato, limite))));
    }
}
