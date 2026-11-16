namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;
using DTOs.Comprovantes;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Grava o envio já sem recusa: gera o número, submete a inscrição, concilia os arquivos e grava o comprovante. O envio fixa os
/// arquivos que contam e remove os de exigência que não se aplica na versão fixada; no rascunho nada foi apagado, e os objetos do
/// armazenamento saem pela rotina de limpeza, depois do commit.
/// </summary>
public sealed class GravacaoDoEnvio(
    IInscricaoRepository inscricoes,
    IDocumentoDaInscricaoRepository documentos,
    IGeradorDeNumeroInscricao gerador,
    GravacaoDaInscricao gravacao)
{
    public async Task<Result<ReciboDaInscricaoDto>> GravarAsync(
        PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado, PrecondicaoIfMatch precondicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(avaliado);

        IReadOnlyList<ArquivoConfirmado> confirmados = avaliado.Arquivos.Verificados();
        string numero = await gerador.ProximoAsync(cancellationToken).ConfigureAwait(false);
        int jaEmitidos = (await inscricoes.ObterEmissoesAsync([pedido.Inscricao.Id], cancellationToken).ConfigureAwait(false)).Count;
        Result<ComprovanteInscricao> submetido = Submeter(pedido, avaliado, numero, precondicao, confirmados);
        return submetido.IsFailure
            ? Result<ReciboDaInscricaoDto>.ValidationFailure(submetido.Errors)
            : await SalvarAsync(pedido, avaliado, submetido.Value!, confirmados, jaEmitidos, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ComprovanteInscricao> Submeter(
        PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado, string numero, PrecondicaoIfMatch precondicao, IReadOnlyList<ArquivoConfirmado> confirmados) =>
        pedido.Inscricao.Submeter(
            numero, pedido.Request.OfertaCursoId, pedido.Request.OfertaCursoSegundaOpcaoId, pedido.Corpo.Respostas, pedido.Corpo.Grupos,
            pedido.Corpo.Aceites, pedido.AdmitidasDoCandidato, avaliado.Avaliacao, avaliado.Derivados.Valores,
            DerivadosDoSistemaInscricao.DefinicoesDasRespostas, precondicao, avaliado.Envio.Instante, confirmados,
            new MunicipiosConferidos(avaliado.DosMunicipios.Nomes, avaliado.DoEndereco.Cidades, avaliado.Derivados.Nomes));

    private async Task<Result<ReciboDaInscricaoDto>> SalvarAsync(
        PedidoDoEnvio pedido,
        AvaliadoDoEnvio avaliado,
        ComprovanteInscricao comprovante,
        IReadOnlyList<ArquivoConfirmado> confirmados,
        int jaEmitidos,
        CancellationToken cancellationToken)
    {
        await DocumentosEnviadosDaInscricao
            .ConciliarNoEnvioAsync(documentos, avaliado.Arquivos, avaliado.Avaliacao, confirmados, avaliado.Envio.Instante, cancellationToken).ConfigureAwait(false);
        Result salvo = await gravacao.SalvarAsync(pedido.Inscricao, comprovante, cancellationToken).ConfigureAwait(false);

        // O recibo diz qual comprovante abrir: o que ocupa a posição seguinte às já emitidas.
        return salvo.IsFailure
            ? Result<ReciboDaInscricaoDto>.ValidationFailure(salvo.Errors)
            : Result<ReciboDaInscricaoDto>.Success(new ReciboDaInscricaoDto(pedido.Inscricao.Id, comprovante.NumeroInscricao, jaEmitidos + 1, comprovante.Instante));
    }
}
