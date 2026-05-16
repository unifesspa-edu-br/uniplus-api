namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Entities;
using ValueObjects;

/// <summary>
/// Domain service puro que avalia um conjunto de
/// <see cref="ObrigatoriedadeLegal"/> contra um <see cref="Edital"/>
/// (ou sua projeção <see cref="EditalConformidadeView"/>), conforme
/// ADR-0058. Sem dependência de infraestrutura: o avaliador é função pura
/// modulo o logger opcional usado para a variante
/// <see cref="Customizado"/>.
/// </summary>
/// <remarks>
/// <para>Pattern match: 8 cases concretos + catch-all <c>_ => throw</c>
/// (C# não suporta union fechado; CS8509 só dispararia se removêssemos
/// o catch-all, o que silenciaria variantes futuras). A garantia
/// substantiva de exhaustividade vive no fitness reflectivo
/// <c>ValidadorConformidadeEditalExhaustividadeTests</c> em
/// <c>Selecao.Domain.UnitTests</c>: ele itera por reflection cada tipo
/// derivado e exige que o avaliador responda sem cair no catch-all.</para>
/// <para>Comparações textuais usam <see cref="StringComparison.OrdinalIgnoreCase"/>
/// com <c>Trim()</c> nos inputs (admin UI pode chegar com capitalização
/// inconsistente). Predicados cujas listas obrigatórias só contêm
/// whitespace/null são tratados como <em>malformados</em> e reprovam com
/// motivo explícito — não como "tudo presente, lista vazia equivale a
/// nenhum requisito".</para>
/// <para>Avisos diagnósticos (uso de <see cref="Customizado"/>, por
/// exemplo) são propagados via <see cref="ResultadoConformidade.Avisos"/>
/// E também via <see cref="ILogger"/> opcional quando o caller passa um.
/// Application layers que quiserem manter Domain ainda mais frio podem
/// consumir só <see cref="ResultadoConformidade.Avisos"/>.</para>
/// </remarks>
public static partial class ValidadorConformidadeEdital
{
    /// <summary>
    /// Avalia as regras contra o agregado <see cref="Edital"/>. Internamente
    /// projeta para <see cref="EditalConformidadeView"/> via
    /// <see cref="EditalConformidadeView.From(Edital)"/>.
    /// </summary>
    public static ResultadoConformidade Evaluate(
        Edital edital,
        IReadOnlyList<ObrigatoriedadeLegal> regras,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(edital);
        ArgumentNullException.ThrowIfNull(regras);

        return Evaluate(EditalConformidadeView.From(edital), regras, logger);
    }

    /// <summary>
    /// Avalia as regras contra a projeção. Útil em testes (constrói view
    /// diretamente) e em pipelines onde a view já foi materializada.
    /// </summary>
    public static ResultadoConformidade Evaluate(
        EditalConformidadeView view,
        IReadOnlyList<ObrigatoriedadeLegal> regras,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(regras);

        List<RegraAvaliada> avaliadas = new(regras.Count);
        List<string> avisos = [];
        foreach (ObrigatoriedadeLegal regra in regras)
        {
            avaliadas.Add(Avaliar(regra, view, logger, avisos));
        }

        return new ResultadoConformidade(avaliadas, avisos);
    }

    private static RegraAvaliada Avaliar(
        ObrigatoriedadeLegal regra,
        EditalConformidadeView view,
        ILogger? logger,
        List<string> avisos)
    {
        ArgumentNullException.ThrowIfNull(regra);

        (bool aprovada, string motivo) = regra.Predicado switch
        {
            EtapaObrigatoria p => AvaliarEtapa(p, view),
            ModalidadesMinimas p => AvaliarModalidades(p, view),
            DesempateDeveIncluir p => AvaliarDesempate(p, view),
            DocumentoObrigatorioParaModalidade p => AvaliarDocumento(p, view),
            BonusObrigatorio p => AvaliarBonus(p, view),
            AtendimentoDisponivel p => AvaliarAtendimento(p, view),
            ConcorrenciaDuplaObrigatoria => AvaliarConcorrenciaDupla(view),
            Customizado p => AvaliarCustomizado(p, regra, logger, avisos),
            _ => throw new InvalidOperationException(
                $"Variante PredicadoObrigatoriedade não suportada: {regra.Predicado.GetType().Name}. "
                + "Adicione um case explícito em ValidadorConformidadeEdital.Avaliar."),
        };

        string descricao = string.IsNullOrEmpty(motivo)
            ? regra.DescricaoHumana
            : $"{regra.DescricaoHumana} — {motivo}";

        return new RegraAvaliada(
            RegraCodigo: regra.RegraCodigo,
            Aprovada: aprovada,
            BaseLegal: regra.BaseLegal,
            PortariaInterna: regra.PortariaInternaCodigo,
            DescricaoHumana: descricao,
            Hash: HashPlaceholder(regra));
    }

    private static (bool, string) AvaliarEtapa(EtapaObrigatoria p, EditalConformidadeView view)
    {
        if (string.IsNullOrWhiteSpace(p.TipoEtapaCodigo))
            return (false, "Predicado malformado: TipoEtapaCodigo vazio.");

        string alvo = p.TipoEtapaCodigo.Trim();
        bool presente = view.CodigosTiposEtapaPresentes
            .Any(c => string.Equals(c?.Trim(), alvo, StringComparison.OrdinalIgnoreCase));

        return presente
            ? (true, string.Empty)
            : (false, $"Etapa '{alvo}' ausente no edital.");
    }

    private static (bool, string) AvaliarModalidades(ModalidadesMinimas p, EditalConformidadeView view)
    {
        List<string>? exigidos = NormalizarListaObrigatoria(p.Codigos);
        if (exigidos is null)
            return (false, "Predicado malformado: lista de modalidades vazia ou só com itens em branco.");

        HashSet<string> presentes = ConstruirIndice(view.CodigosModalidadesPresentes);
        List<string> ausentes = exigidos.Where(c => !presentes.Contains(c)).ToList();

        return ausentes.Count == 0
            ? (true, string.Empty)
            : (false, $"Modalidade(s) ausente(s): {string.Join(", ", ausentes)}.");
    }

    private static (bool, string) AvaliarDesempate(DesempateDeveIncluir p, EditalConformidadeView view)
    {
        if (string.IsNullOrWhiteSpace(p.Criterio))
            return (false, "Predicado malformado: critério vazio.");

        string alvo = p.Criterio.Trim();
        bool presente = view.CriteriosDesempateConfigurados
            .Any(c => string.Equals(c?.Trim(), alvo, StringComparison.OrdinalIgnoreCase));

        return presente
            ? (true, string.Empty)
            : (false, $"Critério de desempate '{alvo}' não configurado no edital.");
    }

    private static (bool, string) AvaliarDocumento(DocumentoObrigatorioParaModalidade p, EditalConformidadeView view)
    {
        if (string.IsNullOrWhiteSpace(p.Modalidade) || string.IsNullOrWhiteSpace(p.TipoDocumento))
            return (false, "Predicado malformado: Modalidade ou TipoDocumento vazio.");

        string modalidade = p.Modalidade.Trim();
        string tipo = p.TipoDocumento.Trim();
        bool exigido = view.DocumentosObrigatorios.Any(d =>
            d is not null &&
            string.Equals(d.Modalidade?.Trim(), modalidade, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(d.TipoDocumento?.Trim(), tipo, StringComparison.OrdinalIgnoreCase));

        return exigido
            ? (true, string.Empty)
            : (false, $"Documento '{tipo}' não exigido para modalidade '{modalidade}'.");
    }

    private static (bool, string) AvaliarBonus(BonusObrigatorio p, EditalConformidadeView view)
    {
        List<string>? exigidas = NormalizarListaObrigatoria(p.ModalidadesAplicaveis);
        if (exigidas is null)
            return (false, "Predicado malformado: lista de modalidades aplicáveis vazia ou só com itens em branco.");

        HashSet<string> comBonus = ConstruirIndice(view.ModalidadesComBonus);
        List<string> faltando = exigidas.Where(m => !comBonus.Contains(m)).ToList();

        return faltando.Count == 0
            ? (true, string.Empty)
            : (false, $"Bônus não habilitado para: {string.Join(", ", faltando)}.");
    }

    private static (bool, string) AvaliarAtendimento(AtendimentoDisponivel p, EditalConformidadeView view)
    {
        List<string>? exigidas = NormalizarListaObrigatoria(p.Necessidades);
        if (exigidas is null)
            return (false, "Predicado malformado: lista de necessidades vazia ou só com itens em branco.");

        HashSet<string> disponiveis = ConstruirIndice(view.AtendimentosDisponiveis);
        List<string> faltando = exigidas.Where(n => !disponiveis.Contains(n)).ToList();

        return faltando.Count == 0
            ? (true, string.Empty)
            : (false, $"Atendimento(s) ausente(s): {string.Join(", ", faltando)}.");
    }

    private static (bool, string) AvaliarConcorrenciaDupla(EditalConformidadeView view)
    {
        return view.ConcorrenciaDuplaHabilitada
            ? (true, string.Empty)
            : (false, "Concorrência dupla (Lei 14.723/2023) não habilitada no edital.");
    }

    /// <summary>
    /// Variante de escape (ADR-0058 §"válvula de escape"). Para preservar
    /// a integridade da evidência legal, a avaliação é <strong>conservadora
    /// (Aprovada=false)</strong> — o admin que adotou Customizado precisa
    /// substituir por variante tipada ou anexar parecer manual.
    /// Aviso estruturado é propagado via <see cref="ResultadoConformidade.Avisos"/>
    /// e (quando logger fornecido) via <see cref="ILogger"/> warning,
    /// rastreável em telemetria.
    /// </summary>
    private static (bool, string) AvaliarCustomizado(
        Customizado p,
        ObrigatoriedadeLegal regra,
        ILogger? logger,
        List<string> avisos)
    {
        string aviso = $"Predicado.Customizado em uso — RegraCodigo={regra.RegraCodigo} BaseLegal={regra.BaseLegal}. Considere variante tipada.";
        avisos.Add(aviso);

        if (logger is not null)
            LogCustomizadoEmUso(logger, regra.RegraCodigo, regra.BaseLegal);

        // p.Parametros é preservado na regra para snapshot/auditoria; nesta
        // V1 não tentamos interpretar conteúdo arbitrário.
        _ = p.Parametros;

        return (false, "Predicado.Customizado em uso — avaliação manual exigida; resultado conservador.");
    }

    /// <summary>
    /// Normaliza listas obrigatórias: filtra itens null/whitespace, faz
    /// Trim, e retorna <c>null</c> se a lista resultante for vazia
    /// (cenário malformado: o caller pediu requisitos, mas nenhum item
    /// chegou íntegro).
    /// </summary>
    private static List<string>? NormalizarListaObrigatoria(IReadOnlyList<string>? lista)
    {
        if (lista is null || lista.Count == 0)
            return null;

        List<string> normalizados = lista
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

        return normalizados.Count == 0 ? null : normalizados;
    }

    private static HashSet<string> ConstruirIndice(IReadOnlyList<string> origem)
        => new(
            origem.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hash V1 do <see cref="RegraAvaliada"/>. Computa SHA-256 sobre uma
    /// concatenação dos campos textuais do placeholder
    /// <see cref="ObrigatoriedadeLegal"/> <em>e</em> da serialização JSON
    /// do <see cref="PredicadoObrigatoriedade"/> (camelCase, polimórfica),
    /// para que regras com mesmo <c>RegraCodigo</c> mas predicados
    /// diferentes produzam hashes distintos. Não é o hash canônico
    /// determinístico do JSON da regra completa — esse é entregue pela
    /// Story #460 (US-F4-02) junto com a entidade plena. Não usar este
    /// valor como evidência forense estável antes da #460 mergear.
    /// </summary>
    private static string HashPlaceholder(ObrigatoriedadeLegal regra)
    {
        string predicadoJson = JsonSerializer.Serialize<PredicadoObrigatoriedade>(
            regra.Predicado,
            PredicadoObrigatoriedade.JsonOptions);

        string payload = string.Join('|', [
            regra.RegraCodigo,
            regra.BaseLegal,
            regra.PortariaInternaCodigo ?? string.Empty,
            predicadoJson,
        ]);

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        // Convert.ToHexString já produz uppercase — aceitamos para evitar CA1308.
        return Convert.ToHexString(bytes);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Predicado.Customizado em uso — considerar variante tipada. RegraCodigo={RegraCodigo} BaseLegal={BaseLegal}")]
    private static partial void LogCustomizadoEmUso(
        ILogger logger,
        string regraCodigo,
        string baseLegal);
}
