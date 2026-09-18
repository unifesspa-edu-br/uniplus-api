namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

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

        return Projetar(envelope);
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
    /// <c>SELECAO_UNICA</c>/<c>SELECAO_MULTIPLA</c> exige um array com cardinalidade mínima 1 (issue #1077);
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
