namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// A inscrição gravada do candidato, na versão em que ela está (o rascunho adotado na memória, sem gravar), avaliada como se as
/// respostas fossem gravadas no instante, no fuso da janela de inscrição, junto das opções de curso dessa versão.
/// </summary>
public sealed class ConsultaDaInscricaoDoCandidato(
    ICertameDivulgadoRepository certames,
    IProcessoSeletivoRepository processos,
    IDocumentoDaInscricaoRepository documentos,
    IRegistroCodecsEnvelope codecs,
    IResolvedorFusoInstitucional fuso,
    IConferenciaEnderecoGeo geo,
    EnderecosDosDocumentosDoAto enderecos)
{
    public Task<Result<InscricaoDto>> ProjetarAsync(Inscricao inscricao, DateTimeOffset instante, CancellationToken cancellationToken) =>
        PreparacaoDaAvaliacaoDaInscricao.ProjetarInscricaoGravadaAsync(
            inscricao, certames, processos, documentos, codecs, fuso, geo, enderecos, instante, cancellationToken);
}
