namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;

using DTOs;

using Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// Handler do <see cref="ObterFormularioRenderizavelQuery"/> (RN08, UNI-REQ-0072): projeta os
/// blocos <c>formulario</c>/<c>fatosColetados</c> (incluindo <c>valoresSelecionaveis</c>) da versão
/// que o certame <b>divulgado</b> serve.
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

        // As regras saem do grafo reidratado, pela mesma montagem da pré-visualização: o front
        // interpreta exatamente o que a API confere (ADR-0139). A reidratação confere a forma de
        // cada regra e a prova dos bytes, que a projeção da apresentação abaixo não refaz.
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

        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(
            avaliavel.Value!.Definicao, DefinicaoDoProcesso.CodigoDaEtapa(query.Finalidade, null), avaliavel.Value.Ofertas);

        // O ato da divulgação é o que publicou os modelos que este formulário oferece.
        DocumentosDoAtoNoAcervo acervo = new(versao.ProcessoSeletivoId, divulgado.AtoCriadorId, enderecoNoAcervo.De);
        return Projetar(envelope, query.Finalidade, acervo, recorte) ?? NaoEncontrado(query.ProcessoSeletivoId);
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
    /// Projeta os blocos <c>formulario</c>/<c>fatosColetados</c> (incluindo
    /// <c>valoresSelecionaveis</c>, issue #1059) do envelope congelado, já com a
    /// <c>SchemaVersion</c> confirmada como reconhecida por <see cref="IRegistroCodecsEnvelope"/>
    /// (ver <see cref="Handle"/>). Guardado contra QUALQUER forma que não seja exatamente a
    /// esperada — versão vigente congelada ANTES de a apresentação existir no envelope (sem as
    /// chaves novas), ou um valor de tipo/nulidade incoerente (só alcançável por uma linha
    /// adulterada diretamente no banco, nunca pelo caminho normal de escrita, que sempre passa
    /// pelo encoder). O registro de codecs confirma a versão, mas decodifica para o grafo de
    /// entidades das seis dimensões (<see cref="EnvelopeReidratado"/>), não para este DTO de
    /// renderização — por isso a leitura abaixo permanece local, mesma disciplina: toda extração
    /// checa presença, tipo e nulidade antes de usar o valor, nunca um cast bruto sobre entrada
    /// que não passou pelo encoder confiável.
    /// </summary>
    /// <remarks>Nulo quando a versão vigente não tem formulário da finalidade pedida.</remarks>
    private static Result<FormularioRenderizavelDto>? Projetar(
        JsonObject envelope, FinalidadeFormulario finalidade, DocumentosDoAtoNoAcervo acervo, RecorteDaFinalidade recorte)
    {
        string token = EstruturaFormulario.ParaToken(finalidade);
        if (!envelope.TryGetPropertyValue("formularios", out JsonNode? formulariosNode) || formulariosNode is not JsonArray formularios)
        {
            return VersaoSemApresentacao();
        }

        JsonObject? formulario = formularios.OfType<JsonObject>()
            .FirstOrDefault(f => TentarString(f, "finalidade", out string finalidadeDoBloco) && finalidadeDoBloco == token);
        if (formulario is null)
        {
            return null;
        }

        if (!TentarStringOpcional(formulario, "titulo", out string? titulo)
            || !TentarTermos(formulario, finalidade, out List<TermoRenderizavelDto> termos)
            || !TentarEtapas(formulario, finalidade, out List<SecaoRenderizavelDto> etapas)
            || !TentarComprovacaoDocumental(envelope, formulario, token, etapas, acervo, out List<ExigenciaDocumentalCertameDto>? comprovacao))
        {
            return VersaoSemApresentacao();
        }

        if (!envelope.TryGetPropertyValue("fatosColetados", out JsonNode? fatosNode) || fatosNode is not JsonArray fatosColetados)
        {
            return VersaoSemApresentacao();
        }

        // Os campos dos outros formulários dão a apresentação dos pressupostos que eles respondem.
        List<FatoFormularioRenderizavelDto> fatos = [];
        Dictionary<string, FatoFormularioRenderizavelDto> deOutrosFormularios = new(StringComparer.Ordinal);
        foreach (JsonNode? item in fatosColetados)
        {
            if (item is not JsonObject doItem
                || !TentarString(doItem, "finalidade", out string finalidadeDoItem)
                || !TentarFato(doItem, out FatoFormularioRenderizavelDto? fato))
            {
                return VersaoSemApresentacao();
            }

            if (finalidadeDoItem == token)
            {
                fatos.Add(fato);
            }
            else
            {
                deOutrosFormularios[fato.FatoCodigo] = fato;
            }
        }

        if (!TentarGrupos(envelope, token, out List<GrupoFormularioRenderizavelDto>? grupos))
        {
            return VersaoSemApresentacao();
        }

        if (!TentarDataReferenciaFatos(envelope, out DateOnly? dataReferenciaFatos))
        {
            return VersaoSemApresentacao();
        }

        IReadOnlyList<PressupostoRenderizavelDto> pressupostos = [.. recorte.Pressupostos.Select(codigo =>
            deOutrosFormularios.TryGetValue(codigo, out FatoFormularioRenderizavelDto? respondido)
                ? new PressupostoRenderizavelDto(
                    codigo, respondido.Rotulo, respondido.TipoRenderizacao, respondido.Formato, respondido.ValoresSelecionaveis, null)
                : new PressupostoRenderizavelDto(
                    codigo, null, null, null, null, DerivadosDoSistema.Dependencias.GetValueOrDefault(codigo)))];
        return Result<FormularioRenderizavelDto>.Success(new FormularioRenderizavelDto(
            token, titulo, etapas, termos, fatos, comprovacao, grupos, recorte.Regras, pressupostos, dataReferenciaFatos));
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

    /// <summary>Um fato coletado do envelope — item ou campo de grupo — na forma de renderização.</summary>
    private static bool TentarFato(JsonObject fato, [NotNullWhen(true)] out FatoFormularioRenderizavelDto? dto)
    {
        dto = null;
        if (!TentarString(fato, "fatoCodigo", out string fatoCodigo)
            || !TentarStringOpcional(fato, "etapaCodigo", out string? etapaCodigo)
            || !TentarStringOpcional(fato, "formato", out string? formato)
            || !TentarInt(fato, "ordem", out int ordem)
            || !TentarString(fato, "rotulo", out string rotulo)
            || !TentarString(fato, "tipoRenderizacao", out string tipoRenderizacao)
            || !TentarStringOpcional(fato, "ajuda", out string? ajuda)
            || !TentarBool(fato, "pedirConfirmacao", out bool pedirConfirmacao)
            || !TentarValoresSelecionaveis(fato, tipoRenderizacao, out List<ValorSelecionavelDto>? valoresSelecionaveis)
            || !FormatoCoerente(tipoRenderizacao, formato))
        {
            return false;
        }

        dto = new FatoFormularioRenderizavelDto(
            fatoCodigo, ordem, rotulo, tipoRenderizacao, valoresSelecionaveis, etapaCodigo, formato, ajuda, pedirConfirmacao);
        return true;
    }

    /// <summary>
    /// Os grupos repetíveis da finalidade (UNI-REQ-0146), com os campos de cada ocorrência na forma
    /// dos itens. O bloco é obrigatório no envelope; ausente, a versão não tem apresentação.
    /// </summary>
    private static bool TentarGrupos(JsonObject envelope, string token, [NotNullWhen(true)] out List<GrupoFormularioRenderizavelDto>? grupos)
    {
        grupos = null;
        if (!envelope.TryGetPropertyValue("gruposColetados", out JsonNode? gruposNode) || gruposNode is not JsonArray array)
        {
            return false;
        }

        List<GrupoFormularioRenderizavelDto> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject grupo || !TentarString(grupo, "finalidade", out string finalidade))
            {
                return false;
            }

            if (finalidade != token)
            {
                continue;
            }

            if (!TentarString(grupo, "codigo", out string codigo)
                || !TentarInt(grupo, "ordem", out int ordem)
                || !TentarStringOpcional(grupo, "etapaCodigo", out string? etapaCodigo)
                || !TentarString(grupo, "rotulo", out string rotulo)
                || !TentarInt(grupo, "minimo", out int minimo)
                || !grupo.ContainsKey("maximo") || !TentarIntOpcional(grupo, "maximo", out int? maximo)
                || !TentarBool(grupo, "incluiCandidato", out bool incluiCandidato)
                || !grupo.TryGetPropertyValue("subitens", out JsonNode? subitensNode) || subitensNode is not JsonArray subitens)
            {
                return false;
            }

            List<FatoFormularioRenderizavelDto> campos = [];
            foreach (JsonNode? campo in subitens)
            {
                // O campo segue o grupo: a mesma finalidade e nenhuma seção própria.
                if (campo is not JsonObject doCampo
                    || !TentarString(doCampo, "finalidade", out string finalidadeDoCampo) || finalidadeDoCampo != token
                    || !TentarFato(doCampo, out FatoFormularioRenderizavelDto? dto) || dto.EtapaCodigo is not null)
                {
                    return false;
                }

                campos.Add(dto);
            }

            // A contagem e a quantidade de campos são as da forma do grupo: fora delas o grupo não se responde.
            if (!FormaDoGrupo.ContagemValida(minimo, maximo) || !FormaDoGrupo.QuantidadeDeCamposValida(campos.Count))
            {
                return false;
            }

            lidos.Add(new GrupoFormularioRenderizavelDto(codigo, ordem, etapaCodigo, rotulo, minimo, maximo, incluiCandidato, campos));
        }

        grupos = lidos;
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

    private static bool TentarInt(JsonObject objeto, string chave, out int valor)
    {
        valor = 0;
        return objeto.TryGetPropertyValue(chave, out JsonNode? node) && node is JsonValue jv && jv.TryGetValue(out valor);
    }

    private static bool TentarBool(JsonObject objeto, string chave, out bool valor)
    {
        valor = false;
        return objeto.TryGetPropertyValue(chave, out JsonNode? node) && node is JsonValue jv && jv.TryGetValue(out valor);
    }

    /// <summary>
    /// Chave presente com <c>null</c> explícito OU valor de texto: sucesso (campo nulável — a
    /// ausência de título/termo é estado válido). Chave ausente ou de outro tipo: falha.
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

    /// <summary>
    /// As seções e os blocos do formulário, na ordem congelada, com o código com que a seção aparece
    /// nas regras; forma inesperada é versão sem apresentação.
    /// </summary>
    private static bool TentarEtapas(JsonObject formulario, FinalidadeFormulario finalidade, out List<SecaoRenderizavelDto> etapas)
    {
        etapas = [];
        if (!formulario.TryGetPropertyValue("etapas", out JsonNode? node) || node is not JsonArray itens)
        {
            return false;
        }

        foreach (JsonNode? item in itens)
        {
            if (item is not JsonObject etapa
                || !TentarString(etapa, "codigo", out string codigo)
                || !TentarInt(etapa, "ordem", out int ordem)
                || !TentarString(etapa, "tipo", out string tipo)
                || !TentarStringOpcional(etapa, "bloco", out string? bloco)
                || !TentarString(etapa, "titulo", out string titulo)
                || !TentarStringOpcional(etapa, "descricao", out string? descricao)
                || !TentarStringOpcional(etapa, "aviso", out string? aviso))
            {
                return false;
            }

            string? codigoNasRegras = tipo == EstruturaFormulario.TipoSecao ? DefinicaoDoProcesso.CodigoDaEtapa(finalidade, codigo) : null;
            etapas.Add(new SecaoRenderizavelDto(codigo, codigoNasRegras, ordem, tipo, bloco, titulo, descricao, aviso));
        }

        return true;
    }

    /// <summary>
    /// Os termos exigidos do formulário (UNI-REQ-0086), na ordem congelada; qualquer forma fora
    /// da esperada é versão sem apresentação.
    /// </summary>
    private static bool TentarTermos(JsonObject formulario, FinalidadeFormulario finalidade, out List<TermoRenderizavelDto> termos)
    {
        termos = [];
        if (!formulario.TryGetPropertyValue("termos", out JsonNode? node) || node is not JsonArray itens)
        {
            return false;
        }

        foreach (JsonNode? item in itens)
        {
            if (item is not JsonObject termo
                || !TentarString(termo, "codigo", out string codigo)
                || !TentarInt(termo, "ordem", out int ordem)
                || !TentarGuid(termo, "termoId", out Guid termoId)
                || !TentarGuid(termo, "versaoId", out Guid versaoId)
                || !TentarString(termo, "nome", out string nome)
                || !TentarString(termo, "texto", out string texto)
                || !TentarString(termo, "baseLegal", out string baseLegal)
                || !TentarString(termo, "formaAceite", out string formaAceite)
                || !TentarString(termo, "hashVersao", out string hashVersao))
            {
                return false;
            }

            termos.Add(new TermoRenderizavelDto(
                codigo, DefinicaoDoProcesso.CodigoDoTermo(finalidade, codigo), ordem, termoId, versaoId, nome, texto, baseLegal, formaAceite, hashVersao));
        }

        return true;
    }

    private static bool TentarIntOpcional(JsonObject objeto, string chave, out int? valor)
    {
        valor = null;
        if (!objeto.TryGetPropertyValue(chave, out JsonNode? node) || node is null)
        {
            return true;
        }

        if (node is not JsonValue numero || !numero.TryGetValue(out int lido))
        {
            return false;
        }

        valor = lido;
        return true;
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
        JsonObject formulario,
        string finalidade,
        List<SecaoRenderizavelDto> etapas,
        DocumentosDoAtoNoAcervo acervo,
        out List<ExigenciaDocumentalCertameDto>? comprovacao)
    {
        comprovacao = null;
        if (!etapas.Exists(static e => e.Bloco == EstruturaFormulario.BlocoComprovacaoDocumental))
        {
            return true;
        }

        // O formulário publicado tem fase, e toda exigência é de uma fase: a falta de qualquer das
        // duas é forma inesperada, e omitir o documento em silêncio esconderia exigência do candidato.
        return TentarGuid(formulario, "faseId", out Guid faseDoFormulario)
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

    /// <summary>O formato existe se, e só se, o campo é de texto.</summary>
    private static bool FormatoCoerente(string tipoRenderizacao, string? formato) =>
        (TipoRenderizacaoCodigo.FromCodigo(tipoRenderizacao) == TipoRenderizacao.Texto) == !string.IsNullOrWhiteSpace(formato);

    /// <summary>
    /// Chave presente e coerente com a bicondicional (issue #1059, UNI-REQ-0072):
    /// <c>SELECAO_UNICA</c>/<c>SELECAO_MULTIPLA</c> exige um array com cardinalidade mínima 1 (issue #1077);
    /// os demais tipos exigem <c>null</c> explícito. <paramref name="tipoRenderizacao"/>
    /// fora do vocabulário fechado falha aqui — sem isso, um token desconhecido cairia no
    /// ramo "não é seleção" por omissão e aceitaria <c>valoresSelecionaveis: null</c> em silêncio,
    /// a mesma forma que <c>EnvelopeCodec.LerFatosColetados</c> recusa (o decoder converte um
    /// token não reconhecido em <c>TipoRenderizacao.Nenhuma</c>, que <c>FatoColetado.Criar</c>
    /// rejeita). Cada item do array exige <c>ordem</c> não negativa e <c>valorCodigo</c> sem
    /// repetição — mesmas recusas do decoder. Chave ausente, forma incoerente com a bicondicional,
    /// ou item malformado dentro do array: falha — nunca convertida silenciosamente em "sem
    /// valores selecionáveis".
    /// </summary>
    private static bool TentarValoresSelecionaveis(
        JsonObject fato, string tipoRenderizacao, out List<ValorSelecionavelDto>? valoresSelecionaveis)
    {
        valoresSelecionaveis = null;
        if (!fato.TryGetPropertyValue("valoresSelecionaveis", out JsonNode? node))
        {
            return false;
        }

        return (TipoRenderizacaoCodigo.FromCodigo(tipoRenderizacao), node) switch
        {
            (TipoRenderizacao.Nenhuma, _) => false,
            (TipoRenderizacao tipo, null) => !tipo.EhSelecao(),
            (TipoRenderizacao tipo, JsonArray array) when tipo.EhSelecao() => TentarValores(array, out valoresSelecionaveis),
            _ => false,
        };
    }

    /// <summary>Os valores de um fato de seleção: ao menos um, cada código uma vez, ordem não negativa.</summary>
    private static bool TentarValores(JsonArray array, out List<ValorSelecionavelDto>? valoresSelecionaveis)
    {
        valoresSelecionaveis = null;
        List<ValorSelecionavelDto> valores = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject valorItem
                || !TentarString(valorItem, "valorCodigo", out string valorCodigo)
                || !TentarStringOpcional(valorItem, "descricao", out string? descricao)
                || !TentarInt(valorItem, "ordem", out int ordem)
                || ordem < 0
                || !codigos.Add(valorCodigo))
            {
                return false;
            }

            valores.Add(new ValorSelecionavelDto(valorCodigo, descricao, ordem));
        }

        // issue #1077: defesa em profundidade — o decoder já recusa um envelope persistido com
        // array vazio para fato de seleção (EnvelopeMalformado), então este ramo é inalcançável
        // em uso normal. Mantido para nunca projetar um seletor sem opção nenhuma caso um
        // chamador futuro monte o JsonObject por outro caminho que não o decoder.
        if (valores.Count == 0)
        {
            return false;
        }

        valoresSelecionaveis = valores;
        return true;
    }
}
