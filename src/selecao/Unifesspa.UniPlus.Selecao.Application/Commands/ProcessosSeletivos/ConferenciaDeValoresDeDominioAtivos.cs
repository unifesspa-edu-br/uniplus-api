namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Recusa o congelamento de uma versão que exporia, ou já cita, um valor INATIVO do domínio
/// fechado de um fato categórico estático (COR_RACA, SEXO, NACIONALIDADE — os que têm
/// <c>FatoValorDominio</c> filhos no catálogo, UNI-REQ-0072). Nada, hoje, filtra por
/// <c>Ativo</c> no caminho de congelamento: nem <see cref="ResolvedorMetadadosFatosCongelados"/>
/// nem o resolvedor de valores selecionáveis do formulário excluem valor inativo — é o
/// vocabulário DECLARADO inteiro que vai para o envelope. Este gate é o que impede um valor
/// inativo de vazar para lá, recusando ANTES da canonicalização.
/// </summary>
/// <remarks>
/// Espelha <see cref="ConferenciaDeColetabilidadeDeFatos"/> em estilo: helper estático
/// compartilhado pelos três handlers que congelam, sem I/O próprio — recebe o catálogo inteiro
/// já lido UMA vez pelo handler (D4-bis), o mesmo compartilhado com a conferência de
/// coletabilidade e os dois resolvedores.
/// </remarks>
internal static class ConferenciaDeValoresDeDominioAtivos
{
    public const string ValorDeDominioInativo = "ProcessoSeletivo.ValorDeDominioInativo";

    public static Result Conferir(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, IReadOnlySet<string>> valoresInativosPorFato = ConstruirMapaDeValoresInativos(catalogo);
        if (valoresInativosPorFato.Count == 0)
        {
            // O estado de hoje: todo valor semeado está ativo. Nenhum I/O adicional além do
            // catálogo já lido pelo handler.
            return Result.Success();
        }

        // Um fato coletado categórico estático que tenha QUALQUER valor inativo no seu
        // vocabulário declarado é recusado — é o vocabulário INTEIRO que vai para o seletor do
        // formulário público, nunca uma versão filtrada por Ativo.
        FatoColetado? fatoColetadoComValorInativo = processo.FatosColetados
            .OrderBy(static f => f.Ordem)
            .FirstOrDefault(f => valoresInativosPorFato.ContainsKey(f.FatoCodigo));
        if (fatoColetadoComValorInativo is not null)
        {
            return Result.Failure(new DomainError(
                ValorDeDominioInativo,
                $"O fato coletado '{fatoColetadoComValorInativo.FatoCodigo}' tem valor inativo no vocabulário " +
                "declarado do catálogo — o vocabulário inteiro seria congelado no seletor do formulário público, " +
                "e o congelamento não persiste uma opção que não está mais ativa para novas seleções."));
        }

        // Independente do que o processo coleta: um predicado (gatilho documental, pré-condição
        // de fato ou regra de derivação) que cite um valor inativo de um fato categórico
        // estático — mesmo de um fato que este processo não coleta — também é recusado.
        foreach ((string Fato, Operador Operador, JsonElement Valor) condicao in PredicadosDoProcesso(processo))
        {
            if (!valoresInativosPorFato.TryGetValue(condicao.Fato, out IReadOnlySet<string>? inativos))
            {
                continue;
            }

            string? valorCitadoInativo = ValoresCitados(condicao.Operador, condicao.Valor).FirstOrDefault(inativos.Contains);
            if (valorCitadoInativo is not null)
            {
                return Result.Failure(new DomainError(
                    ValorDeDominioInativo,
                    $"Um predicado do processo cita '{valorCitadoInativo}', valor inativo do domínio declarado " +
                    $"do fato '{condicao.Fato}' — o congelamento não persiste uma condição que referencia uma " +
                    "opção que não está mais ativa."));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Constrói <c>{fatoCodigo → códigos inativos}</c> somente para fatos categóricos
    /// ESTÁTICOS — os que têm <c>ValoresDominioDeclarados</c> no catálogo. Fato booleano,
    /// numérico e categórico de escopo-processo (CONDICAO_ATENDIMENTO, TIPO_DEFICIENCIA) não
    /// entram no mapa: os valores deles vêm da oferta do próprio processo, não de um vocabulário
    /// global que possa ser marcado inativo.
    /// </summary>
    private static Dictionary<string, IReadOnlySet<string>> ConstruirMapaDeValoresInativos(
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        Dictionary<string, IReadOnlySet<string>> mapa = new(StringComparer.Ordinal);
        foreach (FatoCandidatoView fato in catalogo.Values)
        {
            if (fato.ValoresDominioDeclarados is not { Count: > 0 } declarados)
            {
                continue;
            }

            HashSet<string> inativos = [.. declarados
                .Where(static v => !v.Ativo)
                .Select(static v => v.Codigo)];
            if (inativos.Count > 0)
            {
                mapa[fato.Codigo] = inativos;
            }
        }

        return mapa;
    }

    /// <summary>
    /// Toda condição <c>{fato, operador, valor}</c> do processo — gatilho documental, pré-condição
    /// de fato coletado e regra de derivação — na mesma varredura, porque os três predicados
    /// participam igualmente da recusa (achado 3 do parecer: independente de
    /// <c>FatosColetados.Count</c>).
    /// </summary>
    private static IEnumerable<(string Fato, Operador Operador, JsonElement Valor)> PredicadosDoProcesso(
        ProcessoSeletivo processo)
    {
        IEnumerable<(string, Operador, JsonElement)> deDocumentos = processo.DocumentosExigidos
            .SelectMany(static d => d.Condicoes)
            .Select(static c => (c.Fato, c.Operador, c.Valor));

        IEnumerable<(string, Operador, JsonElement)> dePrecondicoes = processo.FatosColetados
            .SelectMany(static f => f.Precondicoes)
            .Select(static c => (c.Fato, c.Operador, c.Valor));

        IEnumerable<(string, Operador, JsonElement)> deDerivacao = processo.RegrasDerivacao
            .SelectMany(static config => config.Regras)
            .SelectMany(static regra => regra.Condicoes)
            .Select(static c => (c.Fato, c.Operador, c.Valor));

        return deDocumentos.Concat(dePrecondicoes).Concat(deDerivacao);
    }

    /// <summary>
    /// Os códigos que uma condição <c>{fato, operador, valor}</c> cita, POR OPERADOR:
    /// <c>IGUAL</c>/<c>DIFERENTE</c> citam o escalar; <c>EM</c>/<c>NAO_EM</c> citam cada item do
    /// array — ler <c>Valor.GetString()</c> sobre um operador de lista não encontraria nada e
    /// autorizaria a publicação em silêncio.
    /// </summary>
    private static IEnumerable<string> ValoresCitados(Operador operador, JsonElement valor) => operador switch
    {
        Operador.Igual or Operador.Diferente => valor.ValueKind == JsonValueKind.String
            ? [valor.GetString()!]
            : [],
        Operador.Em or Operador.NaoEm => valor.ValueKind == JsonValueKind.Array
            ? valor.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.String).Select(static item => item.GetString()!)
            : [],
        // MAIOR_IGUAL/MENOR_IGUAL são exclusivos de domínio numérico — nunca citam código de
        // valor de domínio categórico.
        _ => [],
    };
}
