namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A resposta ao salvar: a avaliação do que ficou gravado, com os documentos e os arquivos do candidato. A avaliação não se
/// grava: o rascunho a recalcula.
/// </summary>
public sealed class RespostaDoRascunho(IDocumentoDaInscricaoRepository documentos, IConferenciaEnderecoGeo geo, EnderecosDosDocumentosDoAto enderecos)
{
    public async Task<Result<InscricaoDto>> ResponderAsync(PedidoDoRascunho pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        ArquivosDaInscricao arquivos = await new DocumentosEnviadosDaInscricao(documentos).CarregarAsync(pedido.Inscricao.Id, cancellationToken).ConfigureAwait(false);
        Result<ResultadoDaAvaliacaoDaInscricao> avaliada = await AvaliarOGravadoAsync(pedido, arquivos, cancellationToken).ConfigureAwait(false);
        return avaliada.IsFailure
            ? Result<InscricaoDto>.ValidationFailure(avaliada.Errors)
            : await ProjetarAsync(pedido, avaliada.Value!, arquivos, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ResultadoDaAvaliacaoDaInscricao>> AvaliarOGravadoAsync(
        PedidoDoRascunho pedido, ArquivosDaInscricao arquivos, CancellationToken cancellationToken) =>
        await new PreparacaoDaAvaliacaoDaInscricao(geo).AvaliarAsync(
            pedido.Inscricao,
            pedido.Contexto,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            pedido.Envio,
            arquivos,
            cancellationToken).ConfigureAwait(false);

    private async Task<Result<InscricaoDto>> ProjetarAsync(
        PedidoDoRascunho pedido, ResultadoDaAvaliacaoDaInscricao avaliada, ArquivosDaInscricao arquivos, CancellationToken cancellationToken)
    {
        DocumentosDoAtoNoAcervo acervo = await enderecos
            .DoAtoAsync(pedido.Inscricao.ProcessoSeletivoId, pedido.Contexto.AtoCriadorId, cancellationToken)
            .ConfigureAwait(false);
        Result<AvaliacaoDaInscricaoDto> projetada = ProjecaoDaAvaliacaoDaInscricao.Projetar(avaliada, pedido.Contexto, acervo, arquivos.ParaOCandidato());
        return projetada.IsFailure
            ? Result<InscricaoDto>.ValidationFailure(projetada.Errors)
            : Result<InscricaoDto>.Success(InscricaoDto.De(
                pedido.Inscricao, projetada.Value!, pedido.Contexto.Admitidas.NumeroDeOpcoesDeCurso, pedido.Contexto.OfertasDeCurso));
    }
}
