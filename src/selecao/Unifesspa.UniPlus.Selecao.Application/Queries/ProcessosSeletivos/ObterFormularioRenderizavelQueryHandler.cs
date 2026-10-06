namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Handler do <see cref="ObterFormularioRenderizavelQuery"/> (RN08, UNI-REQ-0072): projeta o
/// formulário renderizável da finalidade a partir da versão que o certame <b>divulgado</b> serve —
/// a apresentação e as regras saem do grafo reidratado, pela mesma projeção do rascunho
/// (<see cref="ProjecaoDoFormularioRenderizavel"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolve pela divulgação, e não pelo relógio.</b> A leitura pública do certame é uma consulta à
/// tabela de divulgações, cuja existência de linha É a publicidade. Enquanto o ato de uma
/// retificação não se confirma, a divulgação não avança: a página do certame serve a versão
/// anterior, que é a que tem publicidade. Resolver o formulário pela versão vigente por relógio o
/// faria servir a versão NOVA — o candidato leria um edital e preencheria o formulário de outro, e
/// os dados seriam coletados sob uma configuração que ainda não tem ato normativo e que pode nunca
/// vir a ter, se o registro for recusado por mérito.
/// </para>
/// <para>
/// <b>Uma recusa só.</b> Processo inexistente, em rascunho, sem versão vigente e sem divulgação
/// caem no mesmo não encontrado — não por uma regra que colapse os casos, mas porque nenhum deles
/// tem linha. Distinguir rascunho de inexistente numa rota anônima é responder a um estranho se um
/// identificador corresponde a um processo que ainda não é público.
/// </para>
/// <para>
/// Antes de projetar, confere a <c>SchemaVersion</c> contra as capacidades de leitura que
/// <see cref="IRegistroCodecsEnvelope"/> declara: sob o regime de codec único reescrito no lugar
/// (ADR-0110 Emenda 2, ADR-0109 Emenda 2), uma versão que deixou de ser a corrente não ganha
/// decodificador próprio, e bytes que coincidentemente têm a forma atual não a tornam reconhecida.
/// </para>
/// </remarks>
public static class ObterFormularioRenderizavelQueryHandler
{
    public static async Task<Result<FormularioRenderizavelDto>> Handle(
        ObterFormularioRenderizavelQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(enderecoNoAcervo);

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        if (divulgado is null)
        {
            return NaoEncontrado(query.ProcessoSeletivoId);
        }

        // A linha aponta o ato que confirmou a publicidade, e é por ele que se chega à versão
        // exata que o certame serve — não à mais nova, que sob retificação pendente ainda não tem
        // publicidade nenhuma.
        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesPorAtoCriadorAsync([divulgado.AtoCriadorId], cancellationToken)
            .ConfigureAwait(false);

        if (versoes.Count == 0)
        {
            // Divulgação sem a versão que ela aponta é corrupção, não ausência: a linha só nasce
            // junto da versão. Recusar como não encontrado esconderia o defeito atrás de uma
            // resposta plausível — e aqui não há oráculo a proteger, porque a existência da linha
            // já disse que o processo é público.
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"A divulgação do processo {query.ProcessoSeletivoId} aponta uma versão de configuração que não existe."));
        }

        VersaoConfiguracao versao = versoes[0];

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
            // sem ser objeto não sobrevive ao cast — os dois viravam 500 num endereço anônimo.
            // São alcançáveis só por linha escrita fora do encoder, e ambos são corrupção, pelo
            // mesmo motivo da versão ausente logo acima.
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"A configuração congelada do processo {query.ProcessoSeletivoId} não é um documento legível."));
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
                query.Finalidade, avaliavel.Value!, grafo.Formularios, grafo.FatosColetados, grafo.GruposColetados, grafo.TermosExigidos,
                dataReferenciaFatos) is not { } formulario)
        {
            return NaoEncontrado(query.ProcessoSeletivoId);
        }

        // O ato da divulgação é o que publicou os modelos que este formulário oferece.
        DocumentosDoAtoNoAcervo acervo = new(versao.ProcessoSeletivoId, divulgado.AtoCriadorId, enderecoNoAcervo.De);
        FormularioProcesso doFormulario = grafo.Formularios.First(f => f.Finalidade == query.Finalidade);
        if (!TentarComprovacaoDocumental(envelope, doFormulario, formulario.Finalidade, acervo, out List<ExigenciaDocumentalCertameDto>? comprovacao))
        {
            return VersaoSemApresentacao();
        }

        return Result<FormularioRenderizavelDto>.Success(new FormularioRenderizavelDto(formulario, comprovacao));
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

    /// <summary>
    /// A recusa única da leitura pública: inexistente, rascunho, sem versão vigente e sem
    /// divulgação recebem a mesma resposta, porque nenhum deles tem linha.
    /// </summary>
    private static Result<FormularioRenderizavelDto> NaoEncontrado(Guid processoSeletivoId) =>
        Result<FormularioRenderizavelDto>.Failure(new DomainError(
            "ProcessoSeletivo.NaoEncontrado",
            $"Processo Seletivo {processoSeletivoId} não encontrado."));

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
