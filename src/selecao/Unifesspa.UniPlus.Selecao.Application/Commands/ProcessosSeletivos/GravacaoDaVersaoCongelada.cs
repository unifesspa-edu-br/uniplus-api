namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Publicacoes.Contracts;

public sealed class GravacaoDaVersaoCongelada(
    IProcessoSeletivoRepository processos,
    IAtoDoProcessoRepository atos,
    IRascunhoDePublicacaoRepository rascunhos,
    ISelecaoUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Grava a versão, o registro do ato e o rascunho de publicação na mesma transação e devolve a
    /// requisição de registro do ato como mensagem durável (ADR-0108): o Wolverine a instala no outbox
    /// dentro da transação que gravou a versão, e ou os dois existem, ou nenhum. O ato ainda não
    /// existe, e não precisa: o id é decidido pelo agregado, e a versão o referencia por valor (ADR-0061).
    /// A recusa do registro do ato vem antes de qualquer escrita (ADR-0125).
    /// </summary>
    internal async Task<(Result Resposta, IEnumerable<object> Eventos)> GravarAsync(
        ProcessoSeletivo processo,
        VersaoConfiguracao versao,
        AtoEDocumentoConferidos ato,
        DadosEdital dados,
        RetificacaoInfo? retificacao,
        CancellationToken cancellationToken)
    {
        Result<AtoDoProcesso> registroDoAto = RegistroDoAtoDaVersao.Montar(
            processo, versao, ato.Ato, ato.TipoDoAto, dados, ato.Documento.Id, retificacao?.EditalRetificadoId, retificacao?.Motivo, timeProvider);
        if (registroDoAto.IsFailure)
        {
            return (Result.ValidationFailure(registroDoAto.Errors), []);
        }

        Result gravada = await GravarEApagarORascunhoAsync(processo, versao, registroDoAto.Value!, cancellationToken).ConfigureAwait(false);
        return gravada.IsFailure
            ? (gravada, [])
            : (Result.Success(), MensagensDaPublicacao.Montar(
                processo, versao, ato.Ato, ato.TipoDoAto, dados.Numero, ato.Documento.HashSha256!, retificacao?.EditalRetificadoId, retificacao?.Motivo));
    }

    // O registro do ato vai na mesma gravação da versão: a consulta dos atos do processo lê só ele, e a confirmação do
    // Publicações o encontra pelo id do ato. Só com a versão gravada o rascunho perde a razão de existir: apagar antes
    // do flush destruiria a transcrição do operador numa recusa posterior. Todo caminho que registra ato apaga, porque
    // a raiz é soft-deletable e o cascade da chave estrangeira jamais dispara.
    private async Task<Result> GravarEApagarORascunhoAsync(
        ProcessoSeletivo processo, VersaoConfiguracao versao, AtoDoProcesso registroDoAto, CancellationToken cancellationToken)
    {
        await processos.AdicionarVersaoConfiguracaoAsync(versao, cancellationToken).ConfigureAwait(false);
        await atos.AdicionarAsync(registroDoAto, cancellationToken).ConfigureAwait(false);

        Result salva = await SalvarAsync(cancellationToken).ConfigureAwait(false);
        if (salva.IsSuccess)
        {
            await rascunhos.ApagarDoProcessoAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        }

        return salva;
    }

    // Duas transições concorrentes do mesmo processo derivam o mesmo número de versão, e o índice único e o gatilho de
    // sucessão deixam passar uma só (ADR-0102): é aqui que a linearidade da cadeia é garantida. A recusa não devolve
    // exceção, e o orquestrador descarta o rastreamento antes de o Wolverine salvar de novo (ADR-0119).
    private async Task<Result> SalvarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && VersaoConfiguracaoConstraintViolation.Traduzir(constraint) is { } erroVersao)
        {
            return Result.Failure(erroVersao);
        }

        return Result.Success();
    }
}
