namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="ObterFormularioRenderizavelQuery"/> (Story #559/#1059, RN08, UNI-REQ-0072):
/// resolve a versão vigente da configuração e projeta os blocos <c>formulario</c>/
/// <c>fatosColetados</c> (incluindo <c>valoresSelecionaveis</c>) do envelope congelado. Distingue
/// 404 (processo inexistente) de 422 (<c>Snapshot.VigenteAusente</c>) — mesmo contrato de erro de
/// <see cref="ObterSnapshotVigenteQueryHandler"/>. Antes de projetar, confere a
/// <c>SchemaVersion</c> contra as capacidades de leitura que <see cref="IRegistroCodecsEnvelope"/>
/// declara: sob o regime de codec único reescrito no lugar (ADR-0110 Emenda 2, ADR-0109 Emenda 2),
/// uma versão que deixou de ser a corrente não ganha decodificador próprio, e bytes que
/// coincidentemente têm a forma atual não a tornam reconhecida — a recusa é
/// <c>EnvelopeCodec.VersaoDesconhecida</c>, decidida antes de qualquer parse.
/// </summary>
public static class ObterFormularioRenderizavelQueryHandler
{
    public static async Task<Result<FormularioRenderizavelDto>> Handle(
        ObterFormularioRenderizavelQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // Endpoint público de renderização: sempre "agora", nunca um instante passado explícito
        // (esse é o uso forense de ObterSnapshotVigenteQuery, não deste).
        DateTimeOffset instante = timeProvider.GetUtcNow();

        VersaoConfiguracao? versao = await processoSeletivoRepository
            .ObterVersaoVigenteAsync(query.ProcessoSeletivoId, instante, cancellationToken)
            .ConfigureAwait(false);

        if (versao is null)
        {
            bool existe = await processoSeletivoRepository
                .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
                .ConfigureAwait(false);

            return existe
                ? Result<FormularioRenderizavelDto>.Failure(new DomainError(
                    "Snapshot.VigenteAusente",
                    $"Nenhuma publicação vigente para o instante {instante:O}."))
                : Result<FormularioRenderizavelDto>.Failure(new DomainError(
                    "ProcessoSeletivo.NaoEncontrado",
                    $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        if (!VersaoReconhecidaParaLeitura(registroCodecs, versao.SchemaVersion))
        {
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                $"A versão '{versao.SchemaVersion}' do envelope congelado não está entre as capacidades de " +
                "leitura reconhecidas pelo codec vivo — mesmo que os bytes tenham a forma atual, uma versão " +
                "aposentada não é reidratada."));
        }

        JsonObject envelope = (JsonObject)JsonNode.Parse(versao.ConfiguracaoCongelada)!;
        return Projetar(envelope);
    }

    /// <summary>
    /// A <paramref name="schemaVersion"/> está entre as capacidades que o registro de codecs hoje
    /// sabe LER? Comparação ordinal — <c>SchemaVersion</c> é token, não texto localizável, e
    /// <c>"0.0.7"</c> não é apelido de <c>"0.0.07"</c> nem de variante de caixa.
    /// </summary>
    private static bool VersaoReconhecidaParaLeitura(IRegistroCodecsEnvelope registroCodecs, string schemaVersion) =>
        registroCodecs.Capacidades.Any(capacidade =>
            string.Equals(capacidade.SchemaVersion, schemaVersion, StringComparison.Ordinal) && capacidade.TemDecoder);

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
    private static Result<FormularioRenderizavelDto> Projetar(JsonObject envelope)
    {
        if (!envelope.TryGetPropertyValue("formulario", out JsonNode? formularioNode) || formularioNode is not JsonObject formulario
            || !TentarStringOpcional(formulario, "titulo", out string? titulo)
            || !TentarStringOpcional(formulario, "termoAceiteTexto", out string? termoAceiteTexto))
        {
            return VersaoSemApresentacao();
        }

        if (!envelope.TryGetPropertyValue("fatosColetados", out JsonNode? fatosNode) || fatosNode is not JsonArray fatosColetados)
        {
            return VersaoSemApresentacao();
        }

        List<FatoFormularioRenderizavelDto> fatos = [];
        foreach (JsonNode? item in fatosColetados)
        {
            if (item is not JsonObject fato
                || !TentarString(fato, "fatoCodigo", out string fatoCodigo)
                || !TentarInt(fato, "ordem", out int ordem)
                || !TentarString(fato, "rotulo", out string rotulo)
                || !TentarString(fato, "tipoRenderizacao", out string tipoRenderizacao)
                || !TentarBool(fato, "obrigatorio", out bool obrigatorio)
                || !TentarPrecondicao(fato, out List<IReadOnlyList<CondicaoPrecondicaoDto>>? precondicao)
                || !TentarValoresSelecionaveis(fato, tipoRenderizacao, out List<ValorSelecionavelDto>? valoresSelecionaveis))
            {
                return VersaoSemApresentacao();
            }

            fatos.Add(new FatoFormularioRenderizavelDto(
                fatoCodigo, ordem, rotulo, tipoRenderizacao, obrigatorio, precondicao, valoresSelecionaveis));
        }

        return Result<FormularioRenderizavelDto>.Success(new FormularioRenderizavelDto(titulo, termoAceiteTexto, fatos));
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
    /// Chave ausente: sucesso, <see langword="null"/> (fato sem pré-condição). Chave presente com
    /// <c>null</c> explícito: mesmo caso — o encoder nunca emite lista vazia. Chave presente com
    /// outro tipo, ou uma cláusula/condição malformada por dentro: falha — nunca convertida
    /// silenciosamente em "sem pré-condição", que mudaria a semântica do campo para incondicional.
    /// </summary>
    private static bool TentarPrecondicao(JsonObject fato, out List<IReadOnlyList<CondicaoPrecondicaoDto>>? precondicao)
    {
        precondicao = null;
        if (!fato.TryGetPropertyValue("precondicao", out JsonNode? node) || node is null)
        {
            return true;
        }

        if (node is not JsonArray clausulasNode)
        {
            return false;
        }

        List<IReadOnlyList<CondicaoPrecondicaoDto>> clausulas = [];
        foreach (JsonNode? clausulaNode in clausulasNode)
        {
            if (clausulaNode is not JsonArray condicoesNode)
            {
                return false;
            }

            List<CondicaoPrecondicaoDto> condicoes = [];
            foreach (JsonNode? condicaoNode in condicoesNode)
            {
                if (condicaoNode is not JsonObject condicao
                    || !TentarString(condicao, "fato", out string fatoCitado)
                    || !TentarString(condicao, "operador", out string operador)
                    || !condicao.TryGetPropertyValue("valor", out JsonNode? valorNode)
                    || valorNode is null)
                {
                    return false;
                }

                condicoes.Add(new CondicaoPrecondicaoDto(fatoCitado, operador, valorNode.Deserialize<JsonElement>()));
            }

            clausulas.Add(condicoes);
        }

        precondicao = clausulas;
        return true;
    }

    /// <summary>
    /// Chave presente e coerente com a bicondicional (issue #1059, UNI-REQ-0072):
    /// <c>SELECAO_UNICA</c>/<c>SELECAO_MULTIPLA</c> exige um array (possivelmente vazio);
    /// <c>BOOLEANO</c>/<c>NUMERO</c> exige <c>null</c> explícito. <paramref name="tipoRenderizacao"/>
    /// fora dos QUATRO tokens fechados falha aqui — sem isso, um token desconhecido cairia no
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

        bool? ehFatoDeSelecao = tipoRenderizacao switch
        {
            "SELECAO_UNICA" or "SELECAO_MULTIPLA" => true,
            "BOOLEANO" or "NUMERO" => false,
            _ => null,
        };

        if (ehFatoDeSelecao is not { } fatoDeSelecao)
        {
            return false;
        }

        if (!fato.TryGetPropertyValue("valoresSelecionaveis", out JsonNode? node))
        {
            return false;
        }

        if (node is null)
        {
            return !fatoDeSelecao;
        }

        if (node is not JsonArray array || !fatoDeSelecao)
        {
            return false;
        }

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

        valoresSelecionaveis = valores;
        return true;
    }
}
