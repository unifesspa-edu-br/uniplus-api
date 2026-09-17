namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
                $"A versão '{versao.SchemaVersion}' do envelope congelado não está entre as capacidades de " +
                "leitura reconhecidas pelo codec vivo."));
        }

        JsonObject envelope = (JsonObject)JsonNode.Parse(versao.ConfiguracaoCongelada)!;
        return Projetar(query.ProcessoSeletivoId, versao.AtoCriadorId, envelope);
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

    private static bool VersaoReconhecidaParaLeitura(IRegistroCodecsEnvelope registroCodecs, string schemaVersion) =>
        registroCodecs.Capacidades.Any(capacidade =>
            string.Equals(capacidade.SchemaVersion, schemaVersion, StringComparison.Ordinal) && capacidade.TemDecoder);

    /// <summary>
    /// Projeta os blocos públicos do envelope congelado. Cada extração confere presença, tipo e
    /// nulidade antes de usar o valor — nunca um cast bruto. Uma forma inesperada recusa a leitura
    /// inteira em vez de emitir campo silenciosamente vazio: o certame é documento com efeito
    /// jurídico, e meia projeção mente mais que uma recusa.
    /// </summary>
    private static Result<CertamePublicadoDto> Projetar(Guid processoSeletivoId, Guid atoCriadorId, JsonObject envelope)
    {
        if (!TentarObjeto(envelope, "tipoProcesso", out JsonObject? tipoProcesso)
            || !TentarTexto(tipoProcesso, "codigo", out string tipoCodigo)
            || !TentarTexto(tipoProcesso, "nome", out string tipoNome))
        {
            return EnvelopeInesperado("tipoProcesso");
        }

        if (!TentarObjeto(envelope, "periodo", out JsonObject? periodo)
            || !TentarTextoOpcional(periodo, "numero", out string? numero)
            || !TentarInstante(periodo, "inicio", out DateTimeOffset inicio)
            || !TentarInstante(periodo, "fim", out DateTimeOffset fim))
        {
            return EnvelopeInesperado("periodo");
        }

        if (!TentarObjeto(envelope, "localidade", out JsonObject? localidade)
            || !TentarTexto(localidade, "codigoIbge", out string codigoIbge)
            || !TentarTexto(localidade, "nome", out string localidadeNome)
            || !TentarTexto(localidade, "uf", out string uf)
            || !TentarTexto(localidade, "fusoHorario", out string fusoHorario))
        {
            return EnvelopeInesperado("localidade");
        }

        if (!TentarObjeto(envelope, "hashesEdital", out JsonObject? hashes)
            || !TentarIdentificador(hashes, "documentoEditalId", out Guid documentoEditalId)
            || !TentarTexto(hashes, "hashSha256", out string hashSha256))
        {
            return EnvelopeInesperado("hashesEdital");
        }

        if (!TentarOfertas(envelope, out List<Guid>? ofertas))
        {
            return EnvelopeInesperado("ofertas");
        }

        if (!TentarTaxaInscricao(envelope, out TaxaInscricaoCertameDto? taxa))
        {
            return EnvelopeInesperado("taxaInscricao");
        }

        if (!TentarRetificacao(envelope, out RetificacaoCertameDto? retificacao))
        {
            return EnvelopeInesperado("retificacao");
        }

        return Result<CertamePublicadoDto>.Success(new CertamePublicadoDto(
            processoSeletivoId,
            atoCriadorId,
            new TipoProcessoCertameDto(tipoCodigo, tipoNome),
            new PeriodoInscricaoCertameDto(numero, inicio, fim),
            new LocalidadeCertameDto(codigoIbge, localidadeNome, uf, fusoHorario),
            new DocumentoEditalCertameDto(documentoEditalId, hashSha256),
            ofertas,
            taxa,
            retificacao));
    }

    private static Result<CertamePublicadoDto> EnvelopeInesperado(string bloco) =>
        Result<CertamePublicadoDto>.Failure(new DomainError(
            "CertamePublicado.EnvelopeInesperado",
            $"O bloco '{bloco}' da configuração congelada não tem a forma esperada — a leitura pública do certame foi recusada em vez de projetar parcialmente."));

    /// <summary>
    /// Identificadores das ofertas de curso. O envelope os congela como texto, não como número nem
    /// como objeto — a lista é o conjunto distinto e ordenado das ofertas com vagas distribuídas.
    /// </summary>
    private static bool TentarOfertas(JsonObject envelope, [NotNullWhen(true)] out List<Guid>? ofertas)
    {
        ofertas = null;
        if (!envelope.TryGetPropertyValue("ofertas", out JsonNode? node) || node is not JsonArray array)
        {
            return false;
        }

        List<Guid> lidas = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonValue valor
                || !valor.TryGetValue(out string? texto)
                || !Guid.TryParse(texto, CultureInfo.InvariantCulture, out Guid oferta))
            {
                return false;
            }

            lidas.Add(oferta);
        }

        ofertas = lidas;
        return true;
    }

    /// <summary>
    /// Bloco de presença explícita: <c>presente: false</c> significa publicação sem taxa
    /// configurada, e vira ausência do bloco na resposta pública — não um objeto com campos nulos,
    /// que o candidato leria como "taxa de valor desconhecido".
    /// </summary>
    private static bool TentarTaxaInscricao(JsonObject envelope, out TaxaInscricaoCertameDto? taxa)
    {
        taxa = null;
        if (!TentarObjeto(envelope, "taxaInscricao", out JsonObject? bloco)
            || !TentarBooleano(bloco, "presente", out bool presente))
        {
            return false;
        }

        if (!presente)
        {
            return true;
        }

        if (!TentarBooleano(bloco, "cobra", out bool cobra)
            || !TentarTextoOpcional(bloco, "valor", out string? valor)
            || !bloco.TryGetPropertyValue("fundamentos", out JsonNode? fundamentosNode)
            || fundamentosNode is not JsonArray fundamentosArray)
        {
            return false;
        }

        List<string> fundamentos = [];
        foreach (JsonNode? item in fundamentosArray)
        {
            if (item is not JsonValue fundamento || !fundamento.TryGetValue(out string? codigo))
            {
                return false;
            }

            fundamentos.Add(codigo);
        }

        taxa = new TaxaInscricaoCertameDto(cobra, valor, fundamentos);
        return true;
    }

    /// <summary>
    /// Bloco condicional: ausente na publicação original, presente quando o edital foi emendado.
    /// Ausência é sucesso com <see langword="null"/>; presença malformada é falha.
    /// </summary>
    private static bool TentarRetificacao(JsonObject envelope, out RetificacaoCertameDto? retificacao)
    {
        retificacao = null;
        if (!envelope.TryGetPropertyValue("retificacao", out JsonNode? node) || node is null)
        {
            return true;
        }

        if (node is not JsonObject bloco
            || !TentarIdentificador(bloco, "editalRetificadoId", out Guid atoRetificadoId)
            || !TentarTexto(bloco, "motivo", out string motivo))
        {
            return false;
        }

        retificacao = new RetificacaoCertameDto(atoRetificadoId, motivo);
        return true;
    }

    private static bool TentarObjeto(JsonObject objeto, string chave, [NotNullWhen(true)] out JsonObject? valor)
    {
        valor = null;
        if (!objeto.TryGetPropertyValue(chave, out JsonNode? node) || node is not JsonObject encontrado)
        {
            return false;
        }

        valor = encontrado;
        return true;
    }

    private static bool TentarTexto(JsonObject? objeto, string chave, out string valor)
    {
        valor = "";
        return objeto is not null
            && objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor!);
    }

    /// <summary>
    /// Chave presente com <c>null</c> explícito ou com texto: sucesso. Chave ausente ou de outro
    /// tipo: falha — a ausência da chave é forma inesperada, não campo opcional vazio.
    /// </summary>
    private static bool TentarTextoOpcional(JsonObject? objeto, string chave, out string? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        return node is null || (node is JsonValue jv && jv.TryGetValue(out valor));
    }

    private static bool TentarBooleano(JsonObject? objeto, string chave, out bool valor)
    {
        valor = false;
        return objeto is not null
            && objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor);
    }

    private static bool TentarIdentificador(JsonObject? objeto, string chave, out Guid valor)
    {
        valor = Guid.Empty;
        if (!TentarTexto(objeto, chave, out string texto))
        {
            return false;
        }

        return Guid.TryParse(texto, CultureInfo.InvariantCulture, out valor);
    }

    /// <summary>
    /// Instante congelado em forma canônica. Lido com <see cref="DateTimeStyles.RoundtripKind"/>
    /// para não deslocar o valor pelo fuso do processo que lê.
    /// </summary>
    private static bool TentarInstante(JsonObject? objeto, string chave, out DateTimeOffset valor)
    {
        valor = default;
        if (!TentarTexto(objeto, chave, out string texto))
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            texto,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out valor);
    }
}
