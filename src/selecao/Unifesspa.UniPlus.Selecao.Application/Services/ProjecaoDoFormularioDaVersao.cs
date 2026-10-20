namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using DTOs;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Projeta o formulário renderizável de uma finalidade a partir de uma <see cref="VersaoConfiguracao"/>
/// JÁ RESOLVIDA (ADR-0139) — extraído de <c>ObterFormularioRenderizavelQueryHandler</c> (issue #1059,
/// UNI-REQ-0072) para ser reusado por <c>ObterFormularioDaInscricaoQueryHandler</c> (issue #1859):
/// as duas leituras projetam os mesmos bytes congelados do mesmo jeito — a única diferença entre
/// elas é COMO a versão é resolvida (pela divulgação atual, ou pela versão em que está a inscrição:
/// a divulgada no rascunho, a congelada no primeiro envio depois dele), nunca o que se faz com ela depois de resolvida.
/// </summary>
public static class ProjecaoDoFormularioDaVersao
{
    /// <param name="comComprovacaoDocumental">
    /// Se a projeção traz a lista do bloco de comprovação documental. O certame público a traz; o
    /// formulário da inscrição do candidato não, porque os documentos que se aplicam a ele vêm da
    /// avaliação da inscrição, sobre as respostas dele (ADR-0143).
    /// </param>
    public static Result<FormularioRenderizavelDto> Projetar(
        VersaoConfiguracao versao,
        FinalidadeFormulario finalidade,
        Guid atoCriadorId,
        IRegistroCodecsEnvelope registroCodecs,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        bool comComprovacaoDocumental = true)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(enderecoNoAcervo);

        if (!registroCodecs.SabeLer(versao.SchemaVersion))
        {
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                $"A versão '{versao.SchemaVersion}' do envelope congelado não está entre as capacidades de " +
                "leitura reconhecidas pelo codec vivo — mesmo que os bytes tenham a forma atual, uma versão " +
                "aposentada não é reidratada."));
        }

        if (!TentarLerEnvelope(versao.ConfiguracaoCongelada, out JsonObject? envelope))
        {
            // Texto que não fecha como JSON faz JsonNode.Parse LANÇAR, e um documento que fecha
            // sem ser objeto não sobrevive ao cast. São alcançáveis só por linha escrita fora do
            // encoder, e ambos são corrupção.
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"A configuração congelada do processo {versao.ProcessoSeletivoId} não é um documento legível."));
        }

        // A reidratação confere a forma de cada entidade e a prova dos bytes; a apresentação e as
        // regras saem do grafo reidratado, pela mesma projeção do rascunho (ADR-0139).
        Result<EnvelopeReidratado> reidratado = registroCodecs.Reidratar(versao);
        if (reidratado.IsFailure)
        {
            return Result<FormularioRenderizavelDto>.Failure(reidratado.Error!);
        }

        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DoCongelado(reidratado.Value!);
        if (avaliavel.IsFailure)
        {
            return Result<FormularioRenderizavelDto>.Failure(avaliavel.Error!);
        }

        if (!TentarDataReferenciaFatos(envelope, out DateOnly? dataReferenciaFatos))
        {
            return VersaoSemApresentacao();
        }

        GrafoConfiguracao grafo = reidratado.Value!.Grafo;
        if (ProjecaoDoFormularioRenderizavel.Projetar(
                finalidade, avaliavel.Value!, grafo.Formularios, grafo.FatosColetados, grafo.GruposColetados, grafo.TermosExigidos,
                dataReferenciaFatos) is not { } formulario)
        {
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                "FormularioInscricao.FinalidadeSemFormulario",
                $"O processo seletivo {versao.ProcessoSeletivoId} não tem formulário para a finalidade '{finalidade}' nesta versão."));
        }

        DocumentosDoAtoNoAcervo acervo = new(versao.ProcessoSeletivoId, atoCriadorId, enderecoNoAcervo.De);
        FormularioProcesso doFormulario = grafo.Formularios.First(f => f.Finalidade == finalidade);
        List<ExigenciaDocumentalCertameDto>? comprovacao = null;
        if (comComprovacaoDocumental
            && !TentarComprovacaoDocumental(envelope, doFormulario, formulario.Finalidade, acervo, out comprovacao))
        {
            return VersaoSemApresentacao();
        }

        DependenciasDaAvaliacaoDto? dependencias = null;
        DependenciasDaAvaliacaoDto? dependenciasDoFator = null;
        if (finalidade == FinalidadeFormulario.Inscricao)
        {
            ConfiguracaoAvaliavel configuracao = ConfiguracaoAvaliavelDoProcesso.DoCongelado(grafo, avaliavel.Value!);
            DependenciasDaAvaliacao daAvaliacao = Domain.Services.AvaliacaoDaInscricao.Dependencias(configuracao);
            DependenciasDaAvaliacao doFator = Domain.Services.AvaliacaoDaInscricao.DependenciasDoFator(configuracao, grafo.BonusRegional);
            dependencias = new DependenciasDaAvaliacaoDto(daAvaliacao.Fatos, daAvaliacao.Grupos);
            dependenciasDoFator = new DependenciasDaAvaliacaoDto(doFator.Fatos, doFator.Grupos);
        }

        return Result<FormularioRenderizavelDto>.Success(
            FormularioRenderizavelDto.De(formulario, comprovacao, dependencias, dependenciasDoFator, doFormulario.FaseId));
    }

    /// <summary>
    /// A mesma data de referência dos fatos que <see cref="Projetar"/> usa para montar a
    /// apresentação — reusada pelo handler de Submeter (issue #1859) para calcular
    /// <c>FAIXA_ETARIA</c> na data CONGELADA na publicação, nunca na de hoje. Nula quando o
    /// processo não declara referência temporal.
    /// </summary>
    public static Result<DateOnly?> ResolverDataReferenciaFatos(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        if (!TentarLerEnvelope(versao.ConfiguracaoCongelada, out JsonObject? envelope)
            || !TentarDataReferenciaFatos(envelope, out DateOnly? data))
        {
            return Result<DateOnly?>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"A configuração congelada do processo {versao.ProcessoSeletivoId} não é um documento legível."));
        }

        return Result<DateOnly?>.Success(data);
    }

    /// <summary>
    /// Lê o documento congelado como objeto JSON, sem deixar passar exceção de análise.
    /// </summary>
    private static bool TentarLerEnvelope(string congelado, [NotNullWhen(true)] out JsonObject? envelope)
    {
        try
        {
            envelope = JsonNode.Parse(congelado) as JsonObject;
        }
        catch (JsonException)
        {
            envelope = null;
        }

        return envelope is not null;
    }

    /// <summary>
    /// A data de referência dos fatos, já resolvida na publicação, do bloco de documentos exigidos.
    /// Nula quando o processo não declara referência temporal; fora da forma de data, a versão não
    /// tem apresentação.
    /// </summary>
    private static bool TentarDataReferenciaFatos(JsonObject envelope, out DateOnly? data)
    {
        data = null;
        if (!envelope.TryGetPropertyValue("documentosExigidos", out JsonNode? node)
            || node is not JsonObject documentos
            || !TentarStringOpcional(documentos, "dataReferenciaFatos", out string? texto))
        {
            return false;
        }

        if (texto is null)
        {
            return true;
        }

        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly lida))
        {
            return false;
        }

        data = lida;
        return true;
    }

    private static Result<FormularioRenderizavelDto> VersaoSemApresentacao() =>
        Result<FormularioRenderizavelDto>.Failure(new DomainError(
            "FormularioInscricao.VersaoSemApresentacao",
            "A versão publicada vigente foi congelada antes de a apresentação do formulário de inscrição existir — publique uma nova versão para disponibilizar o formulário."));

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
    /// As exigências do bloco de comprovação documental (UNI-REQ-0144): as da fase e da finalidade do
    /// formulário, na forma que o certame publica — rótulo, aplicabilidade, obrigatoriedade e formatos. A condição
    /// de cada uma e a árvore de satisfação não atravessam este endereço anônimo
    /// (<see cref="ClassificacaoDosBlocosDoCertame"/>): quais documentos cabem a um candidato é
    /// resposta da execução, sobre as respostas dele. Só é lida quando o formulário tem o bloco.
    /// </summary>
    private static bool TentarComprovacaoDocumental(
        JsonObject envelope,
        FormularioProcesso formulario,
        string finalidade,
        DocumentosDoAtoNoAcervo acervo,
        out List<ExigenciaDocumentalCertameDto>? comprovacao)
    {
        comprovacao = null;
        if (!formulario.Etapas.Any(static e => e.Bloco == BlocoSistema.ComprovacaoDocumental))
        {
            return true;
        }

        // O formulário publicado tem fase, e toda exigência é de uma fase: a falta de qualquer das
        // duas é forma inesperada, e omitir o documento em silêncio esconderia exigência do candidato.
        return formulario.FaseId is { } faseDoFormulario
            && ProjecaoDoCertamePublicado.TentarExigencias(
                envelope,
                exigencia => DoFormulario(exigencia, faseDoFormulario, finalidade),
                acervo,
                out comprovacao);
    }

    /// <summary>
    /// A exigência é coletada neste formulário: na fase dele e declarada para a finalidade dele, porque
    /// inscrição e isenção podem dividir a fase. A exigência fora de formulário não entra em bloco
    /// nenhum. Sem a fase, sem a chave da finalidade ou com finalidade fora do vocabulário, a forma é
    /// inesperada: nulo, que recusa a leitura em vez de omitir o documento.
    /// </summary>
    private static bool? DoFormulario(JsonObject exigencia, Guid faseDoFormulario, string finalidadeDoFormulario)
    {
        if (!TentarGuid(exigencia, "exigidoNaFaseId", out Guid fase)
            || !TentarStringOpcional(exigencia, "finalidade", out string? finalidade)
            || (finalidade is not null && EstruturaFormulario.FinalidadeDoToken(finalidade) == FinalidadeFormulario.Nenhuma))
        {
            return null;
        }

        return fase == faseDoFormulario && string.Equals(finalidade, finalidadeDoFormulario, StringComparison.Ordinal);
    }
}
