namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>A versão congelada e o ato que a acompanha; os dois últimos campos são do ato emendado, na retificação.</summary>
internal sealed record VersaoCongelada(
    ProcessoSeletivo Processo,
    VersaoConfiguracao Versao,
    DadosDoAto Ato,
    TipoAtoPublicadoView TipoConferido,
    DadosEdital Dados,
    DocumentoAPublicar Documento,
    Guid? AtoRetificadoId,
    string? MotivoRetificacao);

internal static class GravacaoDaVersaoCongelada
{
    /// <summary>
    /// Grava a versão, o registro do ato e o rascunho de publicação na mesma transação e devolve a
    /// requisição de registro do ato como mensagem durável (ADR-0108): o Wolverine a instala no outbox
    /// dentro da transação que gravou a versão, e ou os dois existem, ou nenhum. O ato ainda não
    /// existe, e não precisa: o id é decidido pelo agregado, e a versão o referencia por valor (ADR-0061).
    /// </summary>
    internal static async Task<(Result Resposta, IEnumerable<object> Eventos)> GravarAsync(
        VersaoCongelada congelada,
        IProcessoSeletivoRepository processos,
        IAtoDoProcessoRepository atos,
        IRascunhoDePublicacaoRepository rascunhos,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // A recusa vem antes de qualquer escrita (ADR-0125).
        Result<AtoDoProcesso> registroDoAto = RegistroDoAtoDaVersao.Montar(
            congelada.Processo,
            congelada.Versao,
            congelada.Ato,
            congelada.TipoConferido,
            congelada.Dados,
            congelada.Documento.Id,
            congelada.AtoRetificadoId,
            congelada.MotivoRetificacao,
            timeProvider);
        if (registroDoAto.IsFailure)
        {
            return (Result.ValidationFailure(registroDoAto.Errors), []);
        }

        await processos.AdicionarVersaoConfiguracaoAsync(congelada.Versao, cancellationToken).ConfigureAwait(false);

        // O registro do ato vai na mesma gravação da versão: a consulta dos atos do processo lê só
        // ele, e a confirmação do Publicações o encontra pelo id do ato.
        await atos.AdicionarAsync(registroDoAto.Value!, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && VersaoConfiguracaoConstraintViolation.Traduzir(constraint) is { } erroVersao)
        {
            // Duas transições concorrentes do mesmo processo derivam o mesmo número de versão, e o
            // índice único e o gatilho de sucessão deixam passar uma só (ADR-0102): é aqui que a
            // linearidade da cadeia é garantida. A recusa não devolve exceção, e o envoltório do
            // Handle descarta o rastreamento antes de o Wolverine salvar de novo (ADR-0119).
            return (Result.Failure(erroVersao), []);
        }

        // Só com a versão gravada o rascunho perde a razão de existir: apagar antes do flush
        // destruiria a transcrição do operador numa recusa posterior. Todo caminho que registra ato
        // apaga, porque a raiz é soft-deletable e o cascade da chave estrangeira jamais dispara.
        await rascunhos.ApagarDoProcessoAsync(congelada.Processo.Id, cancellationToken).ConfigureAwait(false);

        return (Result.Success(), MensagensDaPublicacao.Montar(
            congelada.Processo,
            congelada.Versao,
            congelada.Ato,
            congelada.TipoConferido,
            congelada.Dados.Numero,
            congelada.Documento.HashSha256!,
            congelada.AtoRetificadoId,
            congelada.MotivoRetificacao));
    }
}
