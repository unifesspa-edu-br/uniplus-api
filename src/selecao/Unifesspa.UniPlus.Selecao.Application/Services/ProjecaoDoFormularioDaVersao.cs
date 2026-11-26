namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Cronograma;
using Domain.ProcessosSeletivos.Formularios;
using Domain.ValueObjects;

using DTOs;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Projeta o formulário apresentado de um código a partir de uma <see cref="VersaoConfiguracao"/>
/// JÁ RESOLVIDA (ADR-0139), para ser usado por
/// <c>ObterFormulariosDaInscricaoQueryHandler</c> e <c>ObterFormularioApresentadoQueryHandler</c>:
/// as duas leituras projetam os mesmos bytes congelados do mesmo jeito — a única diferença entre
/// elas é COMO a versão é resolvida (pela divulgação atual, ou pela versão em que está a inscrição:
/// a divulgada no rascunho, a congelada no primeiro envio depois dele), nunca o que se faz com ela depois de resolvida.
/// </summary>
public static class ProjecaoDoFormularioDaVersao
{
    /// <param name="acervoDaComprovacao">
    /// Onde estão os modelos dos documentos que o ato da versão publicou, para a lista do bloco de comprovação
    /// documental. O certame público a traz; o formulário da inscrição do candidato não (nulo), porque os documentos
    /// que se aplicam a ele vêm da avaliação da inscrição, sobre as respostas dele (ADR-0143).
    /// </param>
    /// <returns>
    /// Sucesso com o formulário; sucesso com <see langword="null"/> quando a versão não tem o formulário do
    /// <paramref name="formulario"/> (ausência, e não falha de leitura); falha quando o congelado não se deixa ler.
    /// </returns>
    public static Result<FormularioApresentadoDoProcessoDto?> Projetar(
        VersaoConfiguracao versao,
        CodigoDoFormulario formulario,
        IRegistroCodecsEnvelope registroCodecs,
        DocumentosDoAtoNoAcervo? acervoDaComprovacao)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(formulario);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Result<ConfiguracaoLida> lida = LerConfiguracao(versao, registroCodecs);
        return lida.IsFailure
            ? Result<FormularioApresentadoDoProcessoDto?>.Failure(lida.Error!)
            : ProjetarFormulario(lida.Value!, formulario, acervoDaComprovacao);
    }

    /// <summary>
    /// Os formulários que o candidato responde para se inscrever na versão: os obrigatórios ligados a uma fase que
    /// coleta inscrição, em ordem de código, lidos do congelado uma só vez. O certame público e os formulários da
    /// inscrição do candidato não trazem a comprovação documental, que vem da avaliação das respostas dele (ADR-0143).
    /// </summary>
    /// <returns>
    /// Sucesso com a lista, vazia quando a versão não tem formulário de inscrição; falha quando o congelado não se
    /// deixa ler ou quando algum dos formulários não se projeta, sem lista parcial.
    /// </returns>
    public static Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> ProjetarDaInscricao(
        VersaoConfiguracao versao, IRegistroCodecsEnvelope registroCodecs)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Result<ConfiguracaoLida> lida = LerConfiguracao(versao, registroCodecs);
        return lida.IsFailure
            ? Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>.Failure(lida.Error!)
            : ProjetarFormulariosDaInscricao(versao, lida.Value!);
    }

    private static Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> ProjetarFormulariosDaInscricao(
        VersaoConfiguracao versao, ConfiguracaoLida lida)
    {
        List<FormularioApresentadoDoProcessoDto> projetados = [];
        foreach (FormularioProcesso formulario in FormulariosDaInscricaoNaVersao.De(lida.Configuracao))
        {
            Result<FormularioApresentadoDoProcessoDto?> projetado = ProjetarFormulario(lida, formulario.Codigo, acervoDaComprovacao: null);
            if (projetado.IsFailure)
            {
                return Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>.Failure(projetado.Error!);
            }

            projetados.Add(projetado.Value ?? throw new InvalidOperationException(
                $"O formulário '{formulario.Codigo.Valor}' da inscrição do processo {versao.ProcessoSeletivoId} está na versão fixada e não tem apresentação."));
        }

        return Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>.Success(projetados);
    }

    private sealed record ConfiguracaoLida(JsonObject Envelope, ConfiguracaoPublicada Configuracao, DefinicaoAvaliavel Avaliavel, string FusoHorario);

    private static Result<ConfiguracaoLida> LerConfiguracao(VersaoConfiguracao versao, IRegistroCodecsEnvelope registroCodecs)
    {
        Result<JsonObject> envelope = LerEnvelope(versao, registroCodecs);
        return envelope.IsFailure
            ? Result<ConfiguracaoLida>.Failure(envelope.Error!)
            : ReidratarConfiguracao(versao, envelope.Value!, registroCodecs);
    }

    // A reidratação confere a forma de cada entidade e a prova dos bytes; a apresentação e as
    // regras saem do grafo reidratado, pela mesma projeção do rascunho (ADR-0139).
    private static Result<ConfiguracaoLida> ReidratarConfiguracao(
        VersaoConfiguracao versao, JsonObject envelope, IRegistroCodecsEnvelope registroCodecs)
    {
        Result<EnvelopeReidratado> reidratado = registroCodecs.Reidratar(versao);
        if (reidratado.IsFailure)
        {
            return Result<ConfiguracaoLida>.Failure(reidratado.Error!);
        }

        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DoCongelado(reidratado.Value!);
        return avaliavel.IsFailure
            ? Result<ConfiguracaoLida>.Failure(avaliavel.Error!)
            : Result<ConfiguracaoLida>.Success(new ConfiguracaoLida(envelope, reidratado.Value!.Configuracao, avaliavel.Value!, reidratado.Value.FusoHorario));
    }

    private static Result<JsonObject> LerEnvelope(VersaoConfiguracao versao, IRegistroCodecsEnvelope registroCodecs)
    {
        if (!registroCodecs.LeAVersao(versao.SchemaVersion))
        {
            return Result<JsonObject>.Failure(VersaoDoEnvelopeNaoReconhecida(versao));
        }

        return EnvelopeCongelado.TentarLer(versao.ConfiguracaoCongelada, out JsonObject? envelope)
            ? Result<JsonObject>.Success(envelope!)
            : Result<JsonObject>.Failure(DocumentoCongeladoIlegivel(versao));
    }

    private static DomainError VersaoDoEnvelopeNaoReconhecida(VersaoConfiguracao versao) => new(
        ErrosCodecEnvelope.VersaoDesconhecida,
        $"A versão '{versao.SchemaVersion}' do envelope congelado não está entre as capacidades de " +
        "leitura reconhecidas pelo codec vivo — mesmo que os bytes tenham a forma atual, uma versão " +
        "aposentada não é reidratada.");

    // Texto que não fecha como JSON faz JsonNode.Parse LANÇAR, e um documento que fecha
    // sem ser objeto não sobrevive ao cast. São alcançáveis só por linha escrita fora do
    // encoder, e ambos são corrupção.
    private static DomainError DocumentoCongeladoIlegivel(VersaoConfiguracao versao) => new(
        "Snapshot.VigenteAusente",
        $"A configuração congelada do processo {versao.ProcessoSeletivoId} não é um documento legível.");

    private static Result<FormularioApresentadoDoProcessoDto?> ProjetarFormulario(
        ConfiguracaoLida lida, CodigoDoFormulario codigo, DocumentosDoAtoNoAcervo? acervoDaComprovacao)
    {
        if (!TentarDataReferenciaFatos(lida.Envelope, out DateOnly? dataReferenciaFatos))
        {
            return ProjecaoDoCertamePublicado.Recusar<FormularioApresentadoDoProcessoDto?>("referenciaTemporalDosFatos");
        }

        ConfiguracaoPublicada configuracaoPublicada = lida.Configuracao;
        return ProjecaoDoFormularioApresentado.Projetar(
                codigo, lida.Avaliavel, configuracaoPublicada.Coleta, CronogramaDoProcesso.Do(configuracaoPublicada.Cronograma.CronogramaFases), dataReferenciaFatos) is { } formulario
            ? ComporFormulario(lida, codigo, formulario, acervoDaComprovacao)
            : Result<FormularioApresentadoDoProcessoDto?>.Success(null);
    }

    private static Result<FormularioApresentadoDoProcessoDto?> ComporFormulario(
        ConfiguracaoLida lida, CodigoDoFormulario codigo, FormularioApresentado formulario, DocumentosDoAtoNoAcervo? acervoDaComprovacao)
    {
        FormularioProcesso doFormulario = lida.Configuracao.Coleta.FormularioDe(codigo)!;
        List<ExigenciaDocumentalCertameDto>? comprovacao = null;
        if (acervoDaComprovacao is not null
            && !TentarComprovacaoDocumental(lida, doFormulario, acervoDaComprovacao, out comprovacao))
        {
            return ProjecaoDoCertamePublicado.Recusar<FormularioApresentadoDoProcessoDto?>("comprovacaoDocumental");
        }

        (DependenciasDaAvaliacaoDto? dependencias, DependenciasDaAvaliacaoDto? dependenciasDoFator) = DependenciasDaInscricao(lida, doFormulario);
        return Result<FormularioApresentadoDoProcessoDto?>.Success(
            new FormularioApresentadoDoProcessoDto(formulario, doFormulario.Codigo.Valor, doFormulario.FaseId, doFormulario.Obrigatorio, comprovacao, dependencias, dependenciasDoFator));
    }

    // As dependências são as da avaliação da inscrição: cada formulário que a inscrição responde as carrega.
    private static (DependenciasDaAvaliacaoDto? Fatos, DependenciasDaAvaliacaoDto? Fator) DependenciasDaInscricao(
        ConfiguracaoLida lida, FormularioProcesso formulario)
    {
        if (!FormulariosDaInscricaoNaVersao.De(lida.Configuracao).Any(f => f.Codigo == formulario.Codigo))
        {
            return (null, null);
        }

        ConfiguracaoAvaliavel configuracao = ConfiguracaoAvaliavelDoProcesso.DoCongelado(lida.Configuracao, lida.Avaliavel);
        DependenciasDaAvaliacao daAvaliacao = Domain.Services.AvaliacaoDaInscricao.Dependencias(configuracao);
        DependenciasDaAvaliacao doFator = Domain.Services.AvaliacaoDaInscricao.DependenciasDoFator(configuracao, lida.Configuracao.Vagas.Fada);
        return (new DependenciasDaAvaliacaoDto(daAvaliacao.Fatos, daAvaliacao.Grupos), new DependenciasDaAvaliacaoDto(doFator.Fatos, doFator.Grupos));
    }

    /// <summary>
    /// A mesma data de referência dos fatos que <see cref="Projetar"/> usa para montar a
    /// apresentação — reusada pelo handler de Submeter para calcular
    /// <c>FAIXA_ETARIA</c> na data CONGELADA na publicação, nunca na de hoje. Nula quando o
    /// processo não declara referência temporal.
    /// </summary>
    public static Result<DateOnly?> ResolverDataReferenciaFatos(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        if (!EnvelopeCongelado.TentarLer(versao.ConfiguracaoCongelada, out JsonObject? envelope)
            || !TentarDataReferenciaFatos(envelope, out DateOnly? data))
        {
            return Result<DateOnly?>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"A configuração congelada do processo {versao.ProcessoSeletivoId} não é um documento legível."));
        }

        return Result<DateOnly?>.Success(data);
    }

    /// <summary>
    /// A data de referência dos fatos, já resolvida na publicação, do bloco de documentos exigidos.
    /// Nula quando o processo não declara referência temporal; fora da forma de data, a versão não
    /// tem apresentação.
    /// </summary>
    private static bool TentarDataReferenciaFatos(JsonObject envelope, out DateOnly? data)
    {
        data = null;
        if (!TentarTextoDaDataDeReferencia(envelope, out string? texto))
        {
            return false;
        }

        return texto is null || TentarDataIso(texto, out data);
    }

    private static bool TentarTextoDaDataDeReferencia(JsonObject envelope, out string? texto)
    {
        texto = null;
        return envelope.TryGetPropertyValue("documentosExigidos", out JsonNode? node)
            && node is JsonObject documentos
            && TentarStringOpcional(documentos, "dataDeReferenciaDosDados", out texto);
    }

    private static bool TentarDataIso(string texto, out DateOnly? data)
    {
        bool valida = DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly lida);
        data = valida ? lida : null;
        return valida;
    }

    /// <summary>Chave presente com valor de texto: sucesso. Ausente, nula ou de outro tipo: falha.</summary>
    private static bool TentarString(JsonObject objeto, string chave, out string valor)
    {
        valor = "";
        return objeto.TryGetPropertyValue(chave, out JsonNode? node) && node is JsonValue jv && jv.TryGetValue(out valor!);
    }

    /// <summary>
    /// Chave presente com <c>null</c> explícito OU valor de texto: sucesso. Chave ausente ou de outro
    /// tipo: falha.
    /// </summary>
    private static bool TentarStringOpcional(JsonObject objeto, string chave, out string? valor)
    {
        valor = null;
        if (!objeto.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        if (node is null)
        {
            return true;
        }

        return node is JsonValue jv && jv.TryGetValue(out valor);
    }

    private static bool TentarGuid(JsonObject objeto, string chave, out Guid valor)
    {
        valor = Guid.Empty;
        return TentarString(objeto, chave, out string texto) && Guid.TryParse(texto, out valor);
    }

    /// <summary>
    /// As exigências do bloco de comprovação documental (UNI-REQ-0144): as da fase e do código do formulário, na
    /// forma que o certame publica — rótulo, aplicabilidade, obrigatoriedade, formatos, prazo de emissão e validade. A condição
    /// de cada uma e as regras de comprovação não atravessam este endereço anônimo
    /// (<see cref="ClassificacaoDosBlocosDoCertame"/>): quais documentos cabem a um candidato é
    /// resposta da execução, sobre as respostas dele. Só é lida quando o formulário tem o bloco.
    /// </summary>
    private static bool TentarComprovacaoDocumental(
        ConfiguracaoLida lida,
        FormularioProcesso formulario,
        DocumentosDoAtoNoAcervo acervo,
        out List<ExigenciaDocumentalCertameDto>? comprovacao)
    {
        comprovacao = null;
        return !formulario.Paginas.Any(static e => e.Bloco == BlocoSistema.ComprovacaoDocumental)
            || TentarExigenciasDaFase(lida, formulario, acervo, out comprovacao);
    }

    /// <summary>
    /// O formulário publicado tem fase, e toda exigência é de uma fase: a falta de qualquer das duas é forma
    /// inesperada, e omitir o documento em silêncio esconderia exigência do candidato. As datas de referência saem
    /// das fases e do fuso congelados na versão, nunca do cadastro vivo.
    /// </summary>
    private static bool TentarExigenciasDaFase(
        ConfiguracaoLida lida,
        FormularioProcesso formulario,
        DocumentosDoAtoNoAcervo acervo,
        out List<ExigenciaDocumentalCertameDto>? comprovacao)
    {
        comprovacao = null;
        return formulario.FaseId is { } fase
            && CronogramaCongelado.Reidratado(lida.Configuracao.Cronograma.CronogramaFases, lida.FusoHorario) is { } cronograma
            && LeituraDoCertameDocumentos.TentarExigencias(
                lida.Envelope,
                exigencia => DoFormulario(exigencia, fase, formulario.Codigo),
                acervo,
                cronograma,
                out comprovacao);
    }

    /// <summary>
    /// A exigência é coletada neste formulário: na fase dele e declarada com o código dele, porque dois
    /// formulários podem dividir a fase. A exigência fora de formulário não entra em bloco
    /// nenhum. Sem a fase, sem a chave do código ou com código fora do formato, a forma é
    /// inesperada: nulo, que recusa a leitura em vez de omitir o documento.
    /// </summary>
    private static bool? DoFormulario(JsonObject exigencia, Guid faseDoFormulario, CodigoDoFormulario formulario)
    {
        if (!TentarGuid(exigencia, "exigidoNaFaseId", out Guid fase)
            || !TentarStringOpcional(exigencia, "formularioCodigo", out string? codigo)
            || (codigo is not null && !CodigoDoFormulario.Criar(codigo).IsSuccess))
        {
            return null;
        }

        return fase == faseDoFormulario && string.Equals(codigo, formulario.Valor, StringComparison.Ordinal);
    }
}
