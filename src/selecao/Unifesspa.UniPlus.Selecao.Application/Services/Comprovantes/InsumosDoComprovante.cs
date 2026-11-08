namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Tudo o que o comprovante do titular reúne para ser montado: a inscrição, o comprovante lido na posição pedida, a versão
/// fixada em que a inscrição foi enviada, o nome do processo, o nome da unidade
/// administradora (a cópia congelada na criação do processo) e o prazo da correção.
/// </summary>
/// <param name="Prazo">O prazo da correção; nulo quando este não é o comprovante que vale ou o prazo não pôde ser lido.</param>
/// <param name="Cancelamento">O cancelamento da inscrição; nulo enquanto ela está enviada.</param>
internal sealed record InsumosDoComprovante(
    Inscricao Inscricao,
    ComprovanteNaPosicao Posicao,
    EnvelopeReidratado Envelope,
    string NomeDoProcesso,
    string NomeDaUnidadeAdministradora,
    PrazoDaCorrecaoDto? Prazo,
    CancelamentoNoComprovanteDto? Cancelamento)
{
    public ComprovanteInscricao Comprovante => Posicao.Comprovante;
}
