namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Copia ao acervo público os documentos de um ato do processo já publicado e registra os endereços na divulgação do
/// certame, que já nasceu com a entrada do ato sem endereço.
/// </summary>
/// <remarks>
/// <para>
/// O ato que cria versão copia o edital, os anexos e os modelos que a versão congelou; o que não cria versão copia o
/// principal e os anexos que o registro do ato declara. Em ambos o que se publica vem do envelope ou do registro do ato,
/// e do cadastro só se lê onde está a cópia selada privada.
/// </para>
/// <para>
/// É o único caminho de escrita no acervo público (ADR-0132). Falha na cópia lança, nada é registrado, e a reentrega
/// repete encontrando no acervo o que já tinha sido copiado.
/// </para>
/// <para>
/// <b>A cópia vem antes da trava.</b> Ela é demorada e nada rastreia durante ela: o processo só é travado, e a divulgação
/// só é lida para mutação, depois de os arquivos estarem no acervo, para não segurar a trava nem a versão da linha
/// durante a transferência. Essa é a ordem de travas do módulo (processo, depois divulgação). O registro é idempotente
/// por ato: a entrada que já tem endereço não muda, e o ato de uma versão anterior à divulgada tem a entrada preenchida
/// sem tocar na versão vigente.
/// </para>
/// </remarks>
public sealed class CopiaDosDocumentosDoAto(
    IAtoDoProcessoRepository atos,
    IProcessoSeletivoRepository processos,
    IRegistroCodecsEnvelope registroCodecs,
    AcervoDaDivulgacao acervo,
    ICertameDivulgadoRepository certames,
    IUnitOfWork unitOfWork)
{
    internal async Task CopiarERegistrarAsync(CopiarDocumentosDoAtoAoAcervo mensagem, CancellationToken cancellationToken)
    {
        EditalDivulgado? copiado = await CopiarAsync(mensagem.AtoId, cancellationToken).ConfigureAwait(false);
        if (copiado is not null)
        {
            await RegistrarEnderecosAsync(mensagem, copiado, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RegistrarEnderecosAsync(
        CopiarDocumentosDoAtoAoAcervo mensagem, EditalDivulgado copiado, CancellationToken cancellationToken)
    {
        await processos.TravarAsync(mensagem.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);

        CertameDivulgado divulgado = await certames
            .ObterParaMutacaoAsync(mensagem.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"O processo {mensagem.ProcessoSeletivoId} não tem divulgação para receber os endereços do ato {mensagem.AtoId}.");

        if (divulgado.RegistrarEnderecos(mensagem.AtoId, copiado.UrlDownload!, copiado.Documentos!))
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <returns>O edital divulgado, ou <see langword="null"/> quando o ato não tem registro na Seleção.</returns>
    private async Task<EditalDivulgado?> CopiarAsync(Guid atoId, CancellationToken cancellationToken)
    {
        AtoDoProcesso? ato = await atos.ObterComAnexosAsync(atoId, cancellationToken).ConfigureAwait(false);
        if (ato is null)
        {
            return null;
        }

        IReadOnlyList<VersaoConfiguracao> versoes = await processos
            .ObterVersoesPorAtoCriadorAsync([atoId], cancellationToken)
            .ConfigureAwait(false);

        return versoes.Count > 0
            ? await acervo.PublicarVersaoAsync(versoes[0], ato, LerEnvelopeOuFalhar(versoes[0]), cancellationToken).ConfigureAwait(false)
            : await acervo.PublicarAtoAsync(ato, cancellationToken).ConfigureAwait(false);
    }

    private JsonObject LerEnvelopeOuFalhar(VersaoConfiguracao versao)
    {
        if (registroCodecs.SabeLer(versao.SchemaVersion) && EnvelopeCongelado.TentarLer(versao.ConfiguracaoCongelada, out JsonObject? envelope))
        {
            return envelope;
        }

        // O documento congelado existe mas não é legível por ESTE processo. Copiar meia projeção seria pior que não
        // copiar — a ausência é visível e a cópia se repete. O tipo próprio é o que permite à política de reentrega
        // separar este caso da falha transiente: a causa esperada é a janela de um deploy em fases, que dura minutos.
        throw new EnvelopeAindaNaoLegivelException(
            $"Configuração congelada do processo {versao.ProcessoSeletivoId} não pôde ser lida para copiar os documentos do ato (schema {versao.SchemaVersion}).");
    }
}
