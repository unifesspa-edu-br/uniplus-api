namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Handler do <see cref="ObterCertamePublicadoQuery"/>: resolve a versão de configuração vigente,
/// confere que o ato que a criou está registrado e projeta os blocos públicos do envelope
/// congelado.
/// </summary>
/// <remarks>
/// A conferência da <c>SchemaVersion</c> contra as capacidades de leitura declaradas pelo registro
/// de codecs é a mesma do <see cref="ObterFormularioRenderizavelQueryHandler"/>, e pelo mesmo
/// motivo: sob codec único reescrito no lugar, uma versão que deixou de ser a corrente não ganha
/// decodificador próprio, e bytes que coincidentemente têm a forma atual não a tornam reconhecida.
/// <para>
/// Essa recusa <b>não</b> colapsa no não encontrado das demais. Ela é falha de leitura, não
/// ausência: um certame publicado e visível que some da consulta pública porque o codec não sabe
/// lê-lo é defeito, e defeito tem de aflorar em vez de se disfarçar de rascunho.
/// </para>
/// </remarks>
public static class ObterCertamePublicadoQueryHandler
{
    public static async Task<Result<CertamePublicadoDto>> Handle(
        ObterCertamePublicadoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoRegistradoReader atoRegistradoReader,
        IRegistroCodecsEnvelope registroCodecs,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoRegistradoReader);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // Sempre "agora": leitura pública, nunca consulta forense a um instante passado.
        DateTimeOffset instante = timeProvider.GetUtcNow();

        VersaoConfiguracao? versao = await ResolverVersaoPublicamenteVisivelAsync(
            query.ProcessoSeletivoId, instante, processoSeletivoRepository, atoRegistradoReader, cancellationToken)
            .ConfigureAwait(false);

        if (versao is null)
        {
            return NaoEncontrado();
        }

        if (!registroCodecs.SabeLer(versao.SchemaVersion))
        {
            return Result<CertamePublicadoDto>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                // Sem citar a versão: ela é identificador interno do codec, e o chamador anônimo não deve
                // aprender por mensagem de erro qual formato o sistema está lendo hoje.
                "A configuração publicada deste certame não pôde ser lida no formato vigente."));
        }

        if (ProjecaoDoCertamePublicado.TentarLerDocumento(versao.ConfiguracaoCongelada) is not { } envelope)
        {
            return ProjecaoDoCertamePublicado.RecusarDocumentoIlegivel();
        }

        return ProjecaoDoCertamePublicado.Projetar(
            query.ProcessoSeletivoId, versao.AtoCriadorId, versao.HashConfiguracao, envelope);
    }

    /// <summary>
    /// A versão que o público vê: a mais nova, entre as vigentes por relógio, cujo ato criador está
    /// registrado. <see langword="null"/> quando nenhuma tem — o que inclui o processo inexistente,
    /// o processo em rascunho e o excluído logicamente, porque a linhagem vem vazia nos três casos.
    /// </summary>
    /// <remarks>
    /// <b>Por que descer a linhagem em vez de exigir o ato da versão mais nova.</b> Um certame já
    /// publicado é ato público, e torná-lo invisível fere a transparência — é para isso que existe
    /// retificação de ato, e não supressão. Entre a retificação e o dreno da mensagem de registro, e
    /// indefinidamente quando esse registro é recusado por mérito, a versão mais nova não tem ato.
    /// Exigi-lo ali tiraria do ar um edital com inscrições abertas.
    /// <para>
    /// Descer não encobre a retificação: enquanto o ato dela não existe, ela não tem publicidade
    /// nenhuma. O que se serve é o último estado que de fato tem ato normativo — e a recusa do
    /// registro é estado que alguém reconcilia, não algo que o público deva pagar com a ausência do
    /// certame.
    /// </para>
    /// <para>
    /// Na ABERTURA o efeito é o oposto e igualmente correto: nenhuma versão tem ato registrado, a
    /// linhagem não oferece degrau nenhum, e o certame não é divulgado — ele nunca foi público, e
    /// divulgá-lo sem ato normativo é o que o critério existe para impedir.
    /// </para>
    /// </remarks>
    private static async Task<VersaoConfiguracao?> ResolverVersaoPublicamenteVisivelAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoRegistradoReader atoRegistradoReader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LinhagemDeVersao> linhagem = await processoSeletivoRepository
            .ObterLinhagemVigenteAsync(processoSeletivoId, instante, cancellationToken)
            .ConfigureAwait(false);

        if (linhagem.Count == 0)
        {
            return null;
        }

        IReadOnlySet<Guid> registrados = await atoRegistradoReader
            .FiltrarRegistradosAsync([.. linhagem.Select(static degrau => degrau.AtoCriadorId)], cancellationToken)
            .ConfigureAwait(false);

        // A linhagem já vem da mais nova para a mais antiga: o primeiro degrau com ato registrado é
        // o que o público deve ver.
        foreach (LinhagemDeVersao degrau in linhagem)
        {
            if (registrados.Contains(degrau.AtoCriadorId))
            {
                return await processoSeletivoRepository
                    .ObterVersaoPorNumeroAsync(processoSeletivoId, degrau.NumeroVersao, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <summary>
    /// Uma só forma de recusa para processo inexistente, rascunho, ausência de versão vigente e ato
    /// não registrado — inclusive quando o registro foi recusado por mérito e parou na fila morta.
    /// Distinguir qualquer um deles entregaria a um chamador anônimo um oráculo sobre estado
    /// interno, e o caso do ato ausente importa duas vezes: além de não vazar estado, é ele que
    /// impede divulgar certame sem ato normativo correspondente.
    /// </summary>
    private static Result<CertamePublicadoDto> NaoEncontrado() =>
        Result<CertamePublicadoDto>.Failure(new DomainError(
            "ProcessoSeletivo.NaoEncontrado",
            "Processo Seletivo não encontrado."));
}
