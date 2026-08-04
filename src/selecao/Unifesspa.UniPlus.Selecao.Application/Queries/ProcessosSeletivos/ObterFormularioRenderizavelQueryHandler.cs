namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="ObterFormularioRenderizavelQuery"/> (Story #559, RN08): resolve a
/// versão vigente da configuração e projeta os blocos <c>formulario</c>/<c>fatosColetados</c> do
/// envelope congelado. Distingue 404 (processo inexistente) de 422 (<c>Snapshot.VigenteAusente</c>)
/// — mesmo contrato de erro de <see cref="ObterSnapshotVigenteQueryHandler"/>.
/// </summary>
public static class ObterFormularioRenderizavelQueryHandler
{
    public static async Task<Result<FormularioRenderizavelDto>> Handle(
        ObterFormularioRenderizavelQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
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

        JsonObject envelope = (JsonObject)JsonNode.Parse(versao.ConfiguracaoCongelada)!;
        return Projetar(envelope);
    }

    /// <summary>
    /// Projeta os blocos <c>formulario</c>/<c>fatosColetados</c> do envelope congelado. Guardado
    /// contra QUALQUER forma que não seja exatamente a esperada — versão vigente congelada ANTES
    /// desta Story (sem as chaves novas), ou um valor de tipo/nulidade incoerente (só alcançável
    /// por uma linha adulterada diretamente no banco, nunca pelo caminho normal de escrita, que
    /// sempre passa pelo encoder). Nenhum <c>Decodificar</c>/<c>LeitorEnvelope</c> disponível aqui
    /// (a Application não alcança a Infrastructure, dona do codec) — a leitura tipada abaixo é o
    /// substituto local, mesma disciplina: toda extração checa presença, tipo e nulidade antes de
    /// usar o valor, nunca um cast bruto sobre entrada que não passou pelo encoder confiável.
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

        List<FatoColetadoDto> fatos = [];
        foreach (JsonNode? item in fatosColetados)
        {
            if (item is not JsonObject fato
                || !TentarString(fato, "fatoCodigo", out string fatoCodigo)
                || !TentarInt(fato, "ordem", out int ordem)
                || !TentarString(fato, "rotulo", out string rotulo)
                || !TentarString(fato, "tipoRenderizacao", out string tipoRenderizacao)
                || !TentarBool(fato, "obrigatorio", out bool obrigatorio)
                || !TentarPrecondicao(fato, out List<IReadOnlyList<CondicaoPrecondicaoDto>>? precondicao))
            {
                return VersaoSemApresentacao();
            }

            fatos.Add(new FatoColetadoDto(fatoCodigo, ordem, rotulo, tipoRenderizacao, obrigatorio, precondicao));
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
}
