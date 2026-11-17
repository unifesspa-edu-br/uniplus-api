namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

public static class CoberturaDaCascata
{
    /// <summary>
    /// Todas as causas de cobertura da cascata de remanejamento, com erro nomeado. Pública porque a conferência da configuração
    /// a avalia depois das famílias do cronograma. Cada oferta e cada destino deixam de ser avaliados só no que lê o dado
    /// que a própria regra recusou: sem cascata não se avaliam o fallback nem as origens, e a origem sem destino declarado
    /// não tem destino a resolver na oferta.
    /// </summary>
    /// <remarks>
    /// A ordenação é determinística em todos os níveis — ofertas por <c>OfertaCursoOrigemId</c>
    /// (comparação direta de <see cref="Guid"/>), modalidades de cada oferta por <c>Codigo</c>
    /// (<see cref="StringComparer.Ordinal"/>), origens da cascata por <c>ModalidadeOrigemCodigo</c>
    /// (ordinal) e destinos por <c>Ordem</c> — para que a ordem das causas nunca varie conforme a
    /// ordem física de retorno do Postgres (os <c>Include</c> de <c>ComConfiguracao</c> não
    /// aplicam ordem às coleções).
    /// </remarks>
    public static IReadOnlyList<FieldError> CausasDaCascata(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas = [.. estado.DistribuicaoVagas.OrderBy(static o => o.OfertaCursoOrigemId)];

        return CausasDaRecusa.Da([.. ofertas.SelectMany(oferta => DaOferta(estado, oferta)), .. DosDestinos(estado, ofertas)]);
    }

    // cascata de remanejamento de cotas (UNI-REQ-0056): SegueCascata só é coberta pela cascata única do processo quando a
    // oferta usa o regime federal — fora dele, a cascata não tem o que validar.
    private static IEnumerable<DomainError> DaOferta(EstadoDaConfiguracao estado, ConfiguracaoDistribuicaoVagas oferta)
    {
        IReadOnlyList<ModalidadeSelecionada> modalidadesSegueCascata = [.. oferta.Modalidades
            .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
            .OrderBy(static m => m.Codigo, StringComparer.Ordinal)];

        if (modalidadesSegueCascata.Count == 0)
        {
            return [];
        }

        string primeira = modalidadesSegueCascata[0].Codigo;

        return OfertaForaDoRegimeFederal(oferta) ? [ForaDoRegimeFederal(oferta, primeira)]
            : estado.Cascata is null ? [SemCascata(oferta, primeira)]
            : [.. FallbackDaOferta(estado, oferta), .. modalidadesSegueCascata.SelectMany(modalidade => DaOrigem(estado, oferta, modalidade.Codigo))];
    }

    private static IEnumerable<DomainError> FallbackDaOferta(EstadoDaConfiguracao estado, ConfiguracaoDistribuicaoVagas oferta) =>
        FallbackNaoSelecionadoNaOferta(oferta, estado.Cascata!.FallbackCodigo)
            ? [new DomainError(
                "ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta",
                $"O fallback \"{estado.Cascata.FallbackCodigo}\" da cascata não é uma modalidade selecionada na oferta {oferta.OfertaCursoOrigemId}.")]
            : [];

    private static IEnumerable<DomainError> DaOrigem(EstadoDaConfiguracao estado, ConfiguracaoDistribuicaoVagas oferta, string origem) =>
        OrigemNaoDeclaradaNaCascata(estado, origem) ? [OrigemSemDestino(oferta, origem)]
        : DestinoDaOrigemNaoResolvivelNaOferta(estado, oferta, origem) ? [DestinoNaoResolvivel(oferta, origem)]
        : [];

    private static DomainError ForaDoRegimeFederal(ConfiguracaoDistribuicaoVagas oferta, string modalidade) => new(
        "ProcessoSeletivo.CascataForaDoRegimeFederal",
        $"A oferta {oferta.OfertaCursoOrigemId} tem modalidade \"{modalidade}\" com SegueCascata, mas não usa regra de distribuição do ramo federal (Lei 12.711).");

    private static DomainError SemCascata(ConfiguracaoDistribuicaoVagas oferta, string modalidade) => new(
        "ProcessoSeletivo.CascataOrigemAusente",
        $"A oferta {oferta.OfertaCursoOrigemId} tem modalidade \"{modalidade}\" com SegueCascata, mas o processo não tem cascata de remanejamento configurada.");

    private static DomainError OrigemSemDestino(ConfiguracaoDistribuicaoVagas oferta, string origem) => new(
        "ProcessoSeletivo.CascataOrigemAusente",
        $"A oferta {oferta.OfertaCursoOrigemId} tem modalidade \"{origem}\" com SegueCascata, mas a cascata não declara nenhum destino para ela.");

    private static DomainError DestinoNaoResolvivel(ConfiguracaoDistribuicaoVagas oferta, string origem) => new(
        "ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta",
        $"Nenhum destino da origem \"{origem}\" na cascata é uma modalidade selecionada na oferta {oferta.OfertaCursoOrigemId}.");

    private static IEnumerable<DomainError> DosDestinos(EstadoDaConfiguracao estado, IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas)
    {
        if (estado.Cascata is null)
        {
            return [];
        }

        IEnumerable<ModalidadeSelecionada> ofertadas = ofertas.SelectMany(static o => o.Modalidades);
        HashSet<string> todosOsCodigosOfertados = new(ofertadas.Select(static m => m.Codigo), StringComparer.Ordinal);
        HashSet<string> todasAsOrigensSegueCascata = OrigensQueSeguemACascata(ofertadas);

        return estado.Cascata.Destinos
            .OrderBy(static d => d.ModalidadeOrigemCodigo, StringComparer.Ordinal)
            .ThenBy(static d => d.Ordem)
            .SelectMany(destino => DoDestino(destino, todasAsOrigensSegueCascata, todosOsCodigosOfertados));
    }

    private static HashSet<string> OrigensQueSeguemACascata(IEnumerable<ModalidadeSelecionada> ofertadas) => new(
        ofertadas.Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata).Select(static m => m.Codigo),
        StringComparer.Ordinal);

    private static IEnumerable<DomainError> DoDestino(
        DestinoRemanejamento destino, HashSet<string> origensSegueCascata, HashSet<string> codigosOfertados)
    {
        if (!origensSegueCascata.Contains(destino.ModalidadeOrigemCodigo))
        {
            yield return new DomainError(
                "ProcessoSeletivo.CascataOrigemNaoSegueCascata",
                $"A cascata declara a origem \"{destino.ModalidadeOrigemCodigo}\", mas nenhuma oferta do processo a marca como SegueCascata.");
        }

        if (!codigosOfertados.Contains(destino.ModalidadeDestinoCodigo))
        {
            yield return new DomainError(
                "ProcessoSeletivo.CascataDestinoDesconhecido",
                $"A cascata declara o destino \"{destino.ModalidadeDestinoCodigo}\", que não é modalidade selecionada em nenhuma oferta do processo.");
        }
    }

    /// <summary>A oferta exige a cascata única do processo: tem ≥1 modalidade SegueCascata (cascata de remanejamento de cotas, UNI-REQ-0056).</summary>
    internal static bool OfertaExigeCascata(ConfiguracaoDistribuicaoVagas oferta) =>
        oferta.Modalidades.Any(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata);

    /// <summary>A oferta não usa o regime federal (Lei 12.711/2012, em qualquer variação) — condição de <c>CascataForaDoRegimeFederal</c>.</summary>
    internal static bool OfertaForaDoRegimeFederal(ConfiguracaoDistribuicaoVagas oferta) =>
        !RegraDistribuicaoVagasCodigo.EhRamoFederal(oferta.RegraDistribuicao.Codigo);

    /// <summary>O fallback da cascata não é modalidade selecionada na oferta — condição de <c>CascataFallbackNaoSelecionadoNaOferta</c> (nível oferta).</summary>
    internal static bool FallbackNaoSelecionadoNaOferta(ConfiguracaoDistribuicaoVagas oferta, string fallbackCodigo) =>
        !oferta.Modalidades.Select(static m => m.Codigo).Contains(fallbackCodigo, StringComparer.Ordinal);

    /// <summary>A cascata não declara nenhum destino para a origem — condição de <c>CascataOrigemAusente</c> (nível modalidade).</summary>
    internal static bool OrigemNaoDeclaradaNaCascata(EstadoDaConfiguracao estado, string modalidadeOrigemCodigo) =>
        estado.Cascata is not null
        && !estado.Cascata.Destinos.Any(d => string.Equals(d.ModalidadeOrigemCodigo, modalidadeOrigemCodigo, StringComparison.Ordinal));

    /// <summary>Nenhum destino declarado da origem é modalidade selecionada NESTA oferta — condição de <c>CascataFallbackNaoSelecionadoNaOferta</c> (nível modalidade).</summary>
    internal static bool DestinoDaOrigemNaoResolvivelNaOferta(EstadoDaConfiguracao estado, ConfiguracaoDistribuicaoVagas oferta, string modalidadeOrigemCodigo)
    {
        if (estado.Cascata is null)
        {
            return false;
        }

        HashSet<string> codigosDaOferta = new(oferta.Modalidades.Select(static m => m.Codigo), StringComparer.Ordinal);
        return !estado.Cascata.Destinos
            .Where(d => string.Equals(d.ModalidadeOrigemCodigo, modalidadeOrigemCodigo, StringComparison.Ordinal))
            .OrderBy(static d => d.Ordem)
            .Any(d => codigosDaOferta.Contains(d.ModalidadeDestinoCodigo));
    }

    /// <summary>
    /// Existe alguma oferta com modalidade SegueCascata sob regime não-federal — mesmo predicado
    /// de <see cref="OfertaForaDoRegimeFederal"/> que a conferência usa, agregado sobre TODAS as ofertas
    /// (não só a primeira) para o item do checklist estrutural.
    /// </summary>
    internal static bool ExisteCascataForaDoRegimeFederal(EstadoDaConfiguracao estado) =>
        estado.DistribuicaoVagas.Any(o => OfertaExigeCascata(o) && OfertaForaDoRegimeFederal(o));

    /// <summary>Existe alguma origem SegueCascata (cascata ausente, ou destino não declarado) alcançável por uma oferta federal.</summary>
    internal static bool ExisteCascataOrigemAusente(EstadoDaConfiguracao estado)
    {
        foreach (ConfiguracaoDistribuicaoVagas oferta in estado.DistribuicaoVagas)
        {
            if (!OfertaExigeCascata(oferta) || OfertaForaDoRegimeFederal(oferta))
            {
                continue;
            }

            if (estado.Cascata is null)
            {
                return true;
            }

            if (oferta.Modalidades
                .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
                .Any(m => OrigemNaoDeclaradaNaCascata(estado, m.Codigo)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Existe fallback ou destino não resolvível numa oferta federal que exige cascata.</summary>
    internal static bool ExisteCascataFallbackNaoSelecionadoNaOferta(EstadoDaConfiguracao estado)
    {
        foreach (ConfiguracaoDistribuicaoVagas oferta in estado.DistribuicaoVagas)
        {
            if (!OfertaExigeCascata(oferta) || OfertaForaDoRegimeFederal(oferta) || estado.Cascata is null)
            {
                continue;
            }

            if (FallbackNaoSelecionadoNaOferta(oferta, estado.Cascata.FallbackCodigo))
            {
                return true;
            }

            if (oferta.Modalidades
                .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
                .Any(m => !OrigemNaoDeclaradaNaCascata(estado, m.Codigo) && DestinoDaOrigemNaoResolvivelNaOferta(estado, oferta, m.Codigo)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Existe origem declarada na cascata que nenhuma oferta do processo marca como SegueCascata.</summary>
    internal static bool ExisteCascataOrigemNaoSegueCascata(EstadoDaConfiguracao estado)
    {
        if (estado.Cascata is null)
        {
            return false;
        }

        HashSet<string> todasAsOrigensSegueCascata = new(
            estado.DistribuicaoVagas.SelectMany(static o => o.Modalidades)
                .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
                .Select(static m => m.Codigo),
            StringComparer.Ordinal);

        return estado.Cascata.Destinos
            .Select(static d => d.ModalidadeOrigemCodigo)
            .Distinct(StringComparer.Ordinal)
            .Any(origem => !todasAsOrigensSegueCascata.Contains(origem));
    }

    /// <summary>Existe destino declarado na cascata que não é modalidade selecionada em nenhuma oferta do processo.</summary>
    internal static bool ExisteCascataDestinoDesconhecido(EstadoDaConfiguracao estado)
    {
        if (estado.Cascata is null)
        {
            return false;
        }

        HashSet<string> todosOsCodigosOfertados = new(
            estado.DistribuicaoVagas.SelectMany(static o => o.Modalidades).Select(static m => m.Codigo),
            StringComparer.Ordinal);

        return estado.Cascata.Destinos.Any(d => !todosOsCodigosOfertados.Contains(d.ModalidadeDestinoCodigo));
    }
}
