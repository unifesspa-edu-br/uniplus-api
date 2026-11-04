namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura da localidade, do algoritmo de contagem de prazo e do calendário de dias úteis congelado.
/// </summary>
internal static class LeituraDoCalendario
{
    /// <summary>
    /// Calendário congelado por valor (UNI-REQ-0080). Reconstruído pelos mesmos value objects que
    /// a publicação usa, e não por atribuição direta: a assimetria entre escrita e leitura é
    /// invisível ao round-trip byte a byte — o encoder reemitiria o valor sujo tal qual, a prova
    /// passaria, e a configuração restaurada carregaria um calendário que o domínio recusaria
    /// criar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Recusa, nunca normaliza.</b> Aparar espaço ou corrigir caixa aqui faria o valor divergir
    /// dos bytes congelados, e a recanonicalização passaria a recusar um artefato legítimo.
    /// </para>
    /// <para>
    /// A ordem canônica é conferida em vez de reordenada, pela mesma razão: o envelope é comparado
    /// byte a byte, e reordenar em silêncio esconderia um artefato que não foi emitido por este
    /// encoder.
    /// </para>
    /// </remarks>
    internal static CalendarioDiasUteisCongelado? LerCalendarioDiasUteis(
        LeitorEnvelope leitor,
        JsonObject payload,
        IReadOnlyList<FaseCronograma> cronogramaFases,
        IReadOnlyList<EtapaProcesso> etapas)
    {
        if (leitor.ObjetoSePresente(payload, "calendarioDiasUteis") is not { } bloco)
        {
            if (!leitor.Falhou)
            {
                ConferirAusencia(leitor, cronogramaFases, etapas);
            }

            return null;
        }

        leitor.ExigirChaves(bloco, "calendarioDiasUteis", "presente", "origemId", "versaoDataset", "diasNaoUteis");

        Guid origemId = leitor.Identificador(bloco, "origemId", "calendarioDiasUteis");
        string versaoDataset = leitor.TextoNaoVazio(bloco, "versaoDataset", "calendarioDiasUteis");
        JsonArray dias = leitor.Array(bloco, "diasNaoUteis", "calendarioDiasUteis");
        if (leitor.Falhou)
        {
            return null;
        }

        List<DiaNaoUtilCongelado>? congelados = LerDiasNaoUteis(leitor, dias);
        return congelados is null ? null : CriarCalendario(leitor, origemId, versaoDataset, congelados);
    }

    // Nenhuma transição que gera versão publica processo com fase ou etapa que aceita recurso e sem
    // calendário vigente. Um envelope nessa combinação não saiu de publicação legítima, e aceitá-lo
    // restauraria configuração que o gate recusa: o round-trip não acusaria, porque o encoder reemitiria
    // a mesma ausência.
    private static void ConferirAusencia(LeitorEnvelope leitor, IReadOnlyList<FaseCronograma> cronogramaFases, IReadOnlyList<EtapaProcesso> etapas)
    {
        if (Domain.ProcessosSeletivos.Prazos.ContagemDePrazos.ContagemDistingueDiaUtil(cronogramaFases, etapas))
        {
            leitor.Recusar<object>(
                "'calendarioDiasUteis' declara ausência num processo com fase ou etapa que aceita recurso — "
                + "combinação que nenhuma publicação produz.");
        }
    }

    private static List<DiaNaoUtilCongelado>? LerDiasNaoUteis(LeitorEnvelope leitor, JsonArray dias)
    {
        List<DiaNaoUtilCongelado> congelados = [];
        for (int i = 0; i < dias.Count; i++)
        {
            string path = $"calendarioDiasUteis.diasNaoUteis[{i}]";
            JsonObject item = leitor.ItemObjeto(dias, i, path);
            if (leitor.Falhou)
            {
                return null;
            }

            DiaNaoUtilCongelado? dia = LerDiaNaoUtil(leitor, item, path);
            if (dia is null)
            {
                return null;
            }

            congelados.Add(dia);
        }

        return congelados;
    }

    private static DiaNaoUtilCongelado? LerDiaNaoUtil(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "data", "abrangencia", "municipioIbge", "municipioNome", "uf");

        DateOnly data = leitor.Data(item, "data", path);
        string abrangencia = leitor.TextoNaoVazio(item, "abrangencia", path);
        string? municipioIbge = leitor.TextoOpcional(item, "municipioIbge", path);
        string? municipioNome = leitor.TextoOpcional(item, "municipioNome", path);
        string? uf = leitor.TextoOpcional(item, "uf", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<DiaNaoUtilCongelado> dia = DiaNaoUtilCongelado.Criar(data, abrangencia, municipioIbge, municipioNome, uf);
        if (dia.IsFailure)
        {
            return leitor.Recusar<DiaNaoUtilCongelado?>($"'{path}' congelado não é um dia não útil válido: {dia.Error!.Message}");
        }

        // A factory normaliza (apara espaço e sobe a UF), porque é o caminho de escrita. Aqui isso aceitaria
        // em silêncio um artefato que este encoder nunca emitiria; comparar o lido com o normalizado
        // mantém o decoder fail-closed sem abrir mão da fonte única de validação.
        return MesmoTextoOriginal(dia.Value!, abrangencia, municipioIbge, municipioNome, uf)
            ? dia.Value
            : leitor.Recusar<DiaNaoUtilCongelado?>($"'{path}' congelado não está na forma canônica — espaço em volta do valor ou UF fora de caixa alta.");
    }

    private static CalendarioDiasUteisCongelado? CriarCalendario(
        LeitorEnvelope leitor, Guid origemId, string versaoDataset, List<DiaNaoUtilCongelado> congelados)
    {
        // A ordem do artefato tem de ser a canônica. Criar reordena; comparar antes distingue o envelope
        // emitido por este encoder do montado à mão.
        if (!CalendarioDiasUteisCongelado.Ordenar(congelados).SequenceEqual(congelados))
        {
            return leitor.Recusar<CalendarioDiasUteisCongelado?>(
                "'calendarioDiasUteis.diasNaoUteis' não está na ordem canônica (data, abrangência, município, UF).");
        }

        Result<CalendarioDiasUteisCongelado> calendario = CalendarioDiasUteisCongelado.Criar(origemId, versaoDataset, congelados);
        if (calendario.IsFailure)
        {
            return leitor.Recusar<CalendarioDiasUteisCongelado?>($"'calendarioDiasUteis' congelado é inválido: {calendario.Error!.Message}");
        }

        // Mesma razão da conferência por dia: a factory apara espaço da versão.
        return string.Equals(calendario.Value!.VersaoDataset, versaoDataset, StringComparison.Ordinal)
            && string.Equals(HashCanonicalComputer.NormalizeNfc(versaoDataset), versaoDataset, StringComparison.Ordinal)
            ? calendario.Value
            : leitor.Recusar<CalendarioDiasUteisCongelado?>(
                "'calendarioDiasUteis.versaoDataset' não está na forma canônica — espaço em volta do valor "
                + "ou texto fora da normalização Unicode que o encoder aplica.");
    }

    /// <summary>
    /// Se o dia reconstruído reproduz, caractere a caractere, os textos que estavam no envelope.
    /// Divergir significa que a factory normalizou algo — e o artefato não era o que este encoder
    /// emite.
    /// </summary>
    internal static bool MesmoTextoOriginal(
        DiaNaoUtilCongelado dia,
        string abrangencia,
        string? municipioIbge,
        string? municipioNome,
        string? uf) =>
        string.Equals(dia.Abrangencia, abrangencia, StringComparison.Ordinal)
        && string.Equals(dia.MunicipioIbge, municipioIbge, StringComparison.Ordinal)
        && string.Equals(dia.MunicipioNome, municipioNome, StringComparison.Ordinal)
        && string.Equals(dia.Uf, uf, StringComparison.Ordinal)
        // O encoder normaliza o nome do município para NFC. Sem exigir o mesmo aqui, um texto
        // decomposto passaria na comparação — a factory só apara espaço — e a recanonicalização
        // mudaria os bytes, revelando a divergência tarde demais, já na restauração.
        && (municipioNome is null
            || string.Equals(HashCanonicalComputer.NormalizeNfc(municipioNome), municipioNome, StringComparison.Ordinal));

    /// <summary>
    /// Lê o bloco da convenção de contagem congelada (UNI-REQ-0112).
    /// </summary>
    /// <remarks>
    /// <para>
    /// O bloco é fechado nas duas formas: ausente, só a chave de presença; presente, a
    /// identidade inteira. A combinação parcial não é representável — uma versão que
    /// declarasse código sem hash não provaria qual definição aplicou, que é a única razão
    /// de o bloco existir.
    /// </para>
    /// <para>
    /// A referência é reconstruída por <see cref="ReferenciaRegra.Criar"/>, e não por
    /// atribuição direta, pela mesma razão da localidade: um envelope adulterado com hash
    /// fora de forma é malformado, e reidratá-lo daria ao processo restaurado uma
    /// referência que o domínio recusaria criar.
    /// </para>
    /// </remarks>
    internal static ReferenciaRegra? LerAlgoritmoContagemPrazo(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "algoritmoContagemPrazo", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "algoritmoContagemPrazo");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            leitor.ExigirChaves(bloco, "algoritmoContagemPrazo", "presente");
            return null;
        }

        // Esta forma tem uma chave a mais que a tripla, a de presença. Declará-la mantém o fechamento da
        // gramática e a conferência do rol no leitor compartilhado.
        ReferenciaRegra referencia = leitor.RegraDoObjeto(
            bloco, "algoritmoContagemPrazo", [.. AlgoritmoContagemPrazoCodigo.Todos], "presente");

        return leitor.Falhou ? null : referencia;
    }

    /// <summary>
    /// Lê o bloco fechado com a localidade regente e o fuso aplicado (UNI-REQ-0111).
    /// </summary>
    /// <remarks>
    /// A localidade é reconstruída por <see cref="LocalidadeRegente.Criar"/>, e não por atribuição
    /// direta: um envelope adulterado com código fora de forma ou UF incoerente é malformado, e
    /// reidratá-lo daria ao processo restaurado uma localidade que o domínio recusaria criar. O
    /// fuso é validado como zona conhecida aqui mesmo — deixar
    /// <c>FindSystemTimeZoneById</c> estourar depois transformaria envelope adulterado em exceção
    /// sem causa nomeada.
    /// </remarks>
    internal static (LocalidadeRegente? Localidade, string? FusoHorario) LerLocalidade(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "localidade", "$");
        if (leitor.Falhou)
        {
            return (null, null);
        }

        leitor.ExigirChaves(bloco, "localidade", "codigoIbge", "nome", "uf", "fusoHorario");

        string codigoIbge = leitor.TextoNaoVazio(bloco, "codigoIbge", "localidade");
        string nome = leitor.TextoNaoVazio(bloco, "nome", "localidade");
        string uf = leitor.TextoNaoVazio(bloco, "uf", "localidade");
        string fusoHorario = leitor.TextoNaoVazio(bloco, "fusoHorario", "localidade");
        if (leitor.Falhou)
        {
            return (null, null);
        }

        Result<LocalidadeRegente> localidade = LocalidadeRegente.Criar(codigoIbge, nome, uf);
        if (localidade.IsFailure)
        {
            leitor.Recusar<object>($"'localidade' congelada não é uma referência de cidade válida: {localidade.Error!.Message}");
            return (null, null);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fusoHorario, out _))
        {
            leitor.Recusar<object>($"'localidade.fusoHorario' congelado ('{fusoHorario}') não é uma zona reconhecida por este ambiente.");
            return (null, null);
        }

        return (localidade.Value!, fusoHorario);
    }
}
