namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

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

        // Este seletor já recusa o processo excluído logicamente, e é por isso que ele é o
        // caminho — uma consulta própria de versão precisaria repetir essa amarra.
        VersaoConfiguracao? versao = await processoSeletivoRepository
            .ObterVersaoVigenteAsync(query.ProcessoSeletivoId, instante, cancellationToken)
            .ConfigureAwait(false);

        if (versao is null)
        {
            return NaoEncontrado();
        }

        bool atoRegistrado = await atoRegistradoReader
            .EstaRegistradoAsync(versao.AtoCriadorId, cancellationToken)
            .ConfigureAwait(false);

        if (!atoRegistrado)
        {
            return NaoEncontrado();
        }

        if (!VersaoReconhecidaParaLeitura(registroCodecs, versao.SchemaVersion))
        {
            return Result<CertamePublicadoDto>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                // Sem citar a versão: ela é identificador interno do codec, e o chamador anônimo não deve
                // aprender por mensagem de erro qual formato o sistema está lendo hoje.
                "A configuração publicada deste certame não pôde ser lida no formato vigente."));
        }

        if (TentarLerEnvelope(versao.ConfiguracaoCongelada) is not { } envelope)
        {
            return ProjecaoDoCertamePublicado.RecusarDocumentoIlegivel();
        }

        return ProjecaoDoCertamePublicado.Projetar(
            query.ProcessoSeletivoId, versao.AtoCriadorId, versao.HashConfiguracao, envelope);
    }

    /// <summary>
    /// O documento congelado como objeto JSON, ou <see langword="null"/> quando ele não é sequer
    /// isso.
    /// </summary>
    /// <remarks>
    /// Conteúdo que não fecha como JSON, ou que fecha como array ou escalar, só é alcançável por
    /// uma linha adulterada diretamente no banco — nunca pelo caminho de escrita, que sempre passa
    /// pelo canonicalizador. Ainda assim a leitura o trata como recusa: um <c>cast</c> cru viraria
    /// 500 num endereço anônimo, e o resto desta consulta recusa forma inesperada com 422.
    /// </remarks>
    private static JsonObject? TentarLerEnvelope(string configuracaoCongelada)
    {
        try
        {
            return JsonNode.Parse(configuracaoCongelada) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Uma só forma de recusa para processo inexistente, rascunho, ausência de versão vigente e ato
    /// não registrado — inclusive quando o registro foi recusado por mérito e parou na fila morta.
    /// Distinguir qualquer um deles entregaria a um chamador anônimo um oráculo sobre estado
    /// interno, e o caso do ato ausente importa duas vezes: além de não vazar estado, é ele que
    /// impede divulgar certame sem ato normativo correspondente.
    /// <para>
    /// <b>Retirar do ar um certame já visível é comportamento pretendido, não efeito colateral.</b>
    /// Entre a retificação e o registro do novo ato, e indefinidamente quando esse registro é
    /// recusado por mérito, o certame deixa de aparecer. A alternativa — cair para a versão anterior,
    /// cujo ato está registrado — foi avaliada e recusada: ela serviria, por tempo indeterminado, um
    /// edital cuja retificação já vigora, que é encobrir retificação na origem em vez de no cache.
    /// A ADR-0131 fixa a escolha na seção de confirmações, ao mandar que o processo cuja publicação
    /// teve o registro do ato recusado receba a mesma resposta de inexistente e de rascunho.
    /// </para>
    /// </summary>
    private static Result<CertamePublicadoDto> NaoEncontrado() =>
        Result<CertamePublicadoDto>.Failure(new DomainError(
            "ProcessoSeletivo.NaoEncontrado",
            "Processo Seletivo não encontrado."));

    private static bool VersaoReconhecidaParaLeitura(IRegistroCodecsEnvelope registroCodecs, string schemaVersion) =>
        registroCodecs.Capacidades.Any(capacidade =>
            string.Equals(capacidade.SchemaVersion, schemaVersion, StringComparison.Ordinal) && capacidade.TemDecoder);
}
