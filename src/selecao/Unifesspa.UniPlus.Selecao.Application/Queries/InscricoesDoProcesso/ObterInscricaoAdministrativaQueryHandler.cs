namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Wolverine.Attributes;

/// <summary>
/// A inscrição enviada na visão de quem opera o processo, projetada pela permissão da classe de cada dado e de cada
/// arquivo (ADR-0153). A leitura tem dois níveis: o acesso ao resource, decidido antes da busca, e a visibilidade de cada
/// item, decidida antes da montagem da resposta.
/// </summary>
/// <remarks>
/// <para>
/// Nível 1: o handler lê só o vínculo da inscrição, sem decifrar nada. A inscrição que não existe, a de outro processo e
/// a em rascunho respondem 404: sem a permissão da rota na unidade dona, grava a negativa sem o id do resource; com ela,
/// não grava nada. Com o vínculo confirmado, decide o acesso à inscrição, com os dois resultados registrados; sem permissão responde 404 e o conteúdo
/// não é carregado.
/// </para>
/// <para>
/// Nível 2: o handler pede as decisões de todas as classes de dado e de todas as de arquivo, sem
/// olhar o conteúdo, e registra só as permitidas: o registro significa acesso autorizado à classe naquela inscrição, e não
/// dado entregue. Quem não pode ver um item o recebe omitido, sem indicação; o identificador mascarado
/// só indica que pode ser revelado a quem também tem a permissão de revelar. A requisição só segue depois de gravados os registros.
/// </para>
/// </remarks>
public static class ObterInscricaoAdministrativaQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: o verificador de acesso lê as concessões do <c>AutorizacaoDbContext</c> e os repositórios
    /// leem o <c>SelecaoDbContext</c>; o detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<InscricaoAdministrativaDto>> Handle(
        ObterInscricaoAdministrativaQuery query,
        IVerificadorDeAcesso verificador,
        ProjetorDaInscricaoAdministrativa projetor,
        LeituraDaInscricaoAdministrativa leitura,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(projetor);
        ArgumentNullException.ThrowIfNull(leitura);

        ConferenciaDoVinculo conferencia = await leitura
            .ConferirVinculoAsync(query.ProcessoSeletivoId, query.InscricaoId, cancellationToken).ConfigureAwait(false);
        Result<InscricaoAlcancada> alcancada = await AcessoDoOperadorAInscricao
            .AlcancarAsync(verificador, conferencia, query.ProcessoSeletivoId, query.InscricaoId, cancellationToken).ConfigureAwait(false);

        return alcancada.IsFailure
            ? Result<InscricaoAdministrativaDto>.Failure(alcancada.Error!)
            : await ConteudoProjetadoAsync(query.InscricaoId, alcancada.Value!, projetor, leitura, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<InscricaoAdministrativaDto>> ConteudoProjetadoAsync(
        Guid inscricaoId,
        InscricaoAlcancada alcancada,
        ProjetorDaInscricaoAdministrativa projetor,
        LeituraDaInscricaoAdministrativa leitura,
        CancellationToken cancellationToken)
    {
        Result<EnvelopeReidratado> envelope = await leitura.EnvelopeFixadoAsync(alcancada.Vinculo, cancellationToken).ConfigureAwait(false);
        if (envelope.IsFailure)
        {
            return Result<InscricaoAdministrativaDto>.Failure(envelope.Error!);
        }

        ProjecaoDaInscricao projecao = await projetor.ProjetarAsync(alcancada.Resource, cancellationToken).ConfigureAwait(false);
        ConteudoDaInscricao? conteudo = await leitura.LerAsync(inscricaoId, envelope.Value!, projecao, cancellationToken).ConfigureAwait(false);

        return conteudo is null
            ? Result<InscricaoAdministrativaDto>.Failure(AcessoAInscricao.NaoEncontradaNoProcesso)
            : await MontarAsync(alcancada, conteudo, projecao, projetor, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<InscricaoAdministrativaDto>> MontarAsync(
        InscricaoAlcancada alcancada,
        ConteudoDaInscricao conteudo,
        ProjecaoDaInscricao projecao,
        ProjetorDaInscricaoAdministrativa projetor,
        CancellationToken cancellationToken)
    {
        bool podeEmitirComprovante = await projetor.PodeEmitirComprovanteAsync(alcancada.Resource, cancellationToken).ConfigureAwait(false);

        return Result<InscricaoAdministrativaDto>.Success(
            MontadorDaInscricaoAdministrativa.Montar(alcancada.Processo, conteudo, projecao, podeEmitirComprovante));
    }
}
