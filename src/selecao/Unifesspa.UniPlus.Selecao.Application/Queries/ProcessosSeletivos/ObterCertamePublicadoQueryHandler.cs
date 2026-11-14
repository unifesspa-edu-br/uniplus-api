namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="ObterCertamePublicadoQuery"/> e do
/// <see cref="ObterCertamePublicadoPorIdentificadorQuery"/>: lê a divulgação pública do certame,
/// pelo Guid do processo ou pelo identificador legível congelado.
/// </summary>
/// <remarks>
/// <para>
/// Uma consulta, por chave primária ou pelo índice único do identificador. A projeção acontece uma vez, na
/// transação que publica o ato; a leitura só entrega o que já está pronto, e só mostra o endereço dos documentos de um
/// ato depois de a cópia ao acervo registrá-lo. Não há linhagem a resolver, nem pergunta a
/// outro módulo no caminho da requisição, nem documento congelado a interpretar por leitura.
/// </para>
/// <para>
/// <b>A ausência da linha é a única recusa de visibilidade.</b> Processo inexistente, processo em
/// rascunho e processo sem versão vigente devolvem a mesma resposta — não por uma regra que os colapse, mas porque nenhum deles tem linha. Distinguir
/// deixou de ser possível, em vez de ser possível e proibido. A outra recusa possível não é de
/// visibilidade: é a projeção guardada que não se deixa ler, e essa aflora como falha de leitura.
/// </para>
/// </remarks>
public static class ObterCertamePublicadoQueryHandler
{
    public static async Task<Result<CertamePublicadoDto>> Handle(
        ObterCertamePublicadoQuery query,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return Servir(divulgado, query.Instante);
    }

    public static async Task<Result<CertamePublicadoDto>> Handle(
        ObterCertamePublicadoPorIdentificadorQuery query,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraPorIdentificadorAsync(query.IdentificadorLegivel, cancellationToken)
            .ConfigureAwait(false);

        return Servir(divulgado, query.Instante);
    }

    /// <summary>
    /// A leitura comum às duas chaves: a ausência da linha é a única recusa de visibilidade, e uma
    /// projeção que não se deixa ler aflora como falha de leitura.
    /// </summary>
    private static Result<CertamePublicadoDto> Servir(CertameDivulgado? divulgado, DateTimeOffset instante)
    {
        if (divulgado is null)
        {
            return NaoEncontrado();
        }

        if (!LeituraDoDocumentoDivulgado.TentarLerProjecao(divulgado.Certame, out CertamePublicadoDto? certame))
        {
            return LeituraDoDocumentoDivulgado.RecusarDocumentoIlegivel();
        }

        SituacaoDoCertame situacao = SituacaoDaVitrine.Classificar(
            divulgado.InscricoesDe, divulgado.InscricoesAte, instante, SituacaoDaVitrine.LimiarDosUltimosDias);

        // O documento guardado já traz o endereço dos documentos da versão; o público só os vê quando a cópia do ato
        // da versão ao acervo registrou o endereço, para o link nunca apontar para arquivo ausente.
        CertamePublicadoDto servido = divulgado.CopiouOsDocumentosDoAto(certame.AtoCriadorId) ? certame : SemEnderecos(certame);

        return Result<CertamePublicadoDto>.Success(servido with { Situacao = situacao, EditaisPublicados = EditaisServidos(divulgado) });
    }

    private static List<EditalPublicadoCertameDto> EditaisServidos(CertameDivulgado divulgado) =>
        [.. divulgado.EditaisDivulgados
            .OrderBy(static e => e.NumeroVersao ?? int.MaxValue)
            .ThenBy(static e => e.AtoId)
            .Select(ServirEdital)
            .OfType<EditalPublicadoCertameDto>()];

    // Entrada com endereço ilegível fica de fora em vez de derrubar a página inteira: o evento correspondente só perde
    // o link, e o resto do certame continua servido. A entrada sem endereço ainda é servida: o ato existe, e o documento
    // está em preparação.
    private static EditalPublicadoCertameDto? ServirEdital(EditalDivulgado edital)
    {
        if (edital.UrlDownload is null)
        {
            return new EditalPublicadoCertameDto(edital.AtoId, null, []);
        }

        return Uri.TryCreate(edital.UrlDownload, UriKind.Absolute, out Uri? url)
            ? new EditalPublicadoCertameDto(edital.AtoId, url, DocumentosLegiveis(edital))
            : null;
    }

    private static CertamePublicadoDto SemEnderecos(CertamePublicadoDto certame) => certame with
    {
        DocumentoEdital = certame.DocumentoEdital with { UrlDownload = null },
        DocumentosExigidos = [.. certame.DocumentosExigidos.Select(static e => e.Modelo is null ? e : e with { Modelo = e.Modelo with { UrlDownload = null } })],
    };

    /// <summary>
    /// Os documentos do edital com endereço legível; o que não tem fica de fora, como o edital sem
    /// endereço, para um documento ruim não derrubar os demais.
    /// </summary>
    private static List<DocumentoDoAtoCertameDto> DocumentosLegiveis(EditalDivulgado edital)
    {
        List<DocumentoDoAtoCertameDto> documentos = [];
        foreach (DocumentoDivulgado documento in edital.Documentos ?? [])
        {
            if (Uri.TryCreate(documento.UrlDownload, UriKind.Absolute, out Uri? url))
            {
                documentos.Add(new DocumentoDoAtoCertameDto(
                    documento.Papel, documento.Nome, documento.TipoDeMidia, documento.TamanhoBytes, documento.HashSha256, url));
            }
        }

        return documentos;
    }

    private static Result<CertamePublicadoDto> NaoEncontrado() =>
        Result<CertamePublicadoDto>.Failure(new DomainError(
            "ProcessoSeletivo.NaoEncontrado",
            "Processo Seletivo não encontrado."));
}
