namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A resolução de Pesos por Área que a classificação declara e o quadro congelado dela: quando
/// são exigidos, a forma da resolução, o quadro vazio ou com grupo repetido e a lista de áreas
/// do processo.
/// </summary>
internal static class ConferenciasDoQuadroDePesosPorArea
{
    internal const string ErroResolucaoInvalida = "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida";
    internal const string ErroQuadroVazio = "ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio";

    /// <summary>
    /// A classificação calcula a nota pela média ponderada das áreas do ENEM: é a única que usa
    /// o quadro de pesos por área, e por isso a única que exige a resolução de Pesos por Área —
    /// e a única em que cabe uma etapa com a nota vinda do ENEM.
    /// </summary>
    internal static bool Exige(ReferenciaRegra regraCalculo, bool baseadoEmEnem)
    {
        ArgumentNullException.ThrowIfNull(regraCalculo);

        return baseadoEmEnem && regraCalculo.Codigo == RegraDeCalculoDaNota.MediaPonderada.Codigo();
    }

    /// <summary>
    /// Forma da resolução informada: cabe na coluna em NFC, não traz caractere invisível, o nulo
    /// inclusive, e pode ser normalizada (<see cref="TextoNormalizavel.TentarNfc"/>). Resolução
    /// ausente não é assunto daqui: se ela é obrigatória ou indevida depende da classificação.
    /// </summary>
    internal static List<FieldError> ConferirForma(string? resolucao) =>
        string.IsNullOrWhiteSpace(resolucao) || TemFormaValida(resolucao.Trim())
            ? []
            : [Recusa(ErroResolucaoInvalida, $"A resolução de Pesos por Área deve ter até {ConfiguracaoClassificacao.ResolucaoPesoAreaEnemMaxLength} caracteres, sem caracteres de controle, de formatação ou de quebra de linha.")];

    // A resolução volta nas mensagens de recusa e nos logs: além de caber na coluna, não pode
    // trazer quebra de linha nem inversão de direção de leitura.
    private static bool TemFormaValida(string resolucao) =>
        TextoNormalizavel.TentarNfc(resolucao, ConfiguracaoClassificacao.ResolucaoPesoAreaEnemMaxLength, out _)
            == SituacaoDoTexto.Valido;

    /// <summary>
    /// As recusas de uma criação que restam quando a resolução já foi recusada antes, pela forma
    /// ou pelo cadastro. A forma inválida repetida e a falta de quadro congelado são consequência
    /// dessa recusa; qualquer outra violação, inclusive no mesmo campo, continua.
    /// </summary>
    internal static IEnumerable<FieldError> SemConsequenciasDaRecusa(IEnumerable<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);

        return erros.Where(static erro => erro.Error.Code is not (ErroResolucaoInvalida or ErroQuadroVazio));
    }

    /// <summary>
    /// A resolução acompanha a classificação baseada em ENEM com cálculo local, e só ela. A
    /// completude dos grupos é conferida por quem resolve a resolução no cadastro, que conhece
    /// os grupos; aqui fica o que a própria cópia consegue provar: há grupo congelado, e nenhum
    /// se repete.
    /// </summary>
    internal static List<FieldError> Conferir(
        bool exigeQuadro,
        string? resolucao,
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro)
    {
        if (!exigeQuadro)
        {
            return resolucao is not null || quadro.Count > 0
                ? [Recusa("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida", "A resolução de Pesos por Área só se aplica à classificação baseada em ENEM com cálculo local da nota.")]
                : [];
        }

        return resolucao is null
            ? [Recusa("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria", "A classificação baseada em ENEM com cálculo local da nota exige a resolução de Pesos por Área.")]
            : ConferirDeclarada(resolucao, quadro);
    }

    private static List<FieldError> ConferirDeclarada(string resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro)
    {
        List<FieldError> erros = ConferirForma(resolucao);
        if (quadro.Count == 0)
        {
            erros.Add(Recusa(ErroQuadroVazio, "A resolução de Pesos por Área declarada não tem nenhum grupo de área congelado."));
        }

        erros.AddRange(ConferirGruposRepetidos(quadro));
        return erros;
    }

    private static IEnumerable<FieldError> ConferirGruposRepetidos(IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro) =>
        quadro
            .GroupBy(static grupo => grupo.GrupoAreaEnem.Codigo, StringComparer.Ordinal)
            .Where(static grupos => grupos.Count() > 1)
            .Select(static repetido => Recusa(
                "ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido",
                $"O quadro de pesos por área repete o grupo {CaracteresInvisiveis.ParaEco(repetido.First().GrupoAreaEnem.Rotulo, TextoCongelado.TamanhoMaximoEcoado)}."));

    /// <summary>Área do processo é a que todo grupo do quadro de pesos por área tem.</summary>
    internal static HashSet<string> AreasEmTodosOsGrupos(IEnumerable<GrupoPesoAreaEnemCongelado> quadro) =>
        quadro
            .Select(static g => g.Areas.Select(static a => a.Codigo).ToHashSet(StringComparer.Ordinal))
            .Aggregate((comum, doGrupo) =>
            {
                comum.IntersectWith(doGrupo);
                return comum;
            });

    /// <summary>O rótulo sai da primeira ocorrência: o quadro vem de uma resolução só, e cada código tem um rótulo nela.</summary>
    internal static string ListarAreas(IEnumerable<GrupoPesoAreaEnemCongelado> quadro, HashSet<string> areas)
    {
        if (areas.Count == 0)
        {
            return "nenhuma";
        }

        Dictionary<string, string> rotulos = new(StringComparer.Ordinal);
        foreach (AreaPesoAreaEnemCongelada area in quadro.SelectMany(static g => g.Areas))
        {
            rotulos.TryAdd(area.Codigo, area.Rotulo);
        }

        return string.Join("; ", areas
            .Order(StringComparer.Ordinal)
            .Select(codigo => $"{codigo} ({rotulos[codigo]})"));
    }

    private static FieldError Recusa(string codigo, string mensagem) =>
        new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError(codigo, mensagem));
}
