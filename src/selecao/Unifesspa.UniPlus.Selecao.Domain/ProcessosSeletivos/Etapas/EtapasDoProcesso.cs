namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As etapas declaradas de um processo, na ordem em que chegaram. Reúne as regras que dependem de mais de uma etapa, cada
/// uma devolvendo todas as suas causas, e projeta nos itens do checklist as que o painel mostra.
/// </summary>
public sealed record EtapasDoProcesso
{
    public EtapasDoProcesso(IReadOnlyList<EtapaDeclarada> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        Etapas = etapas;
    }

    public IReadOnlyList<EtapaDeclarada> Etapas { get; }

    /// <summary>As etapas do processo vivo, cada uma lida como declarou: as recusas do estado carregado do banco ficam à vista.</summary>
    public static EtapasDoProcesso Das(IEnumerable<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        return new([.. etapas.Select(static etapa => etapa.Declaracao())]);
    }

    /// <summary>A etapa de nota do ENEM, quando o processo declara uma só; com duas, nenhuma é a designada.</summary>
    public EtapaDeclarada? EtapaDeNotaDoEnem
    {
        get
        {
            EtapaDeclarada[] doEnem = [.. Etapas.Where(static etapa => etapa.NotaVemDoEnem)];

            return doEnem.Length == 1 ? doEnem[0] : null;
        }
    }

    public EtapaDeclarada? Etapa(Guid id) => Etapas.FirstOrDefault(etapa => etapa.Id == id);

    /// <summary>As posições, da segunda ocorrência em diante, cujo identificador já apareceu; o que não foi informado não conta.</summary>
    public static IReadOnlyList<int> PosicoesComIdRepetido(IEnumerable<Guid?> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        HashSet<Guid> vistos = [];
        return [.. ids.Select(static (id, posicao) => (id, posicao)).Where(par => par.id.HasValue && !vistos.Add(par.id.Value)).Select(static par => par.posicao)];
    }

    public static DomainError IdRepetido() => new(
        "ProcessoSeletivo.IdEtapaDuplicado",
        "O mesmo identificador de etapa aparece em mais de uma etapa do processo — cada etapa tem identificador próprio.");

    public IReadOnlyList<FieldError> RecusasDasDeclaracoes() =>
    [
        .. Etapas.SelectMany(static (etapa, posicao) => etapa.Recusas.Select(recusa => NaEtapa(posicao, recusa.Field, recusa.Error))),
    ];

    /// <summary>As recusas das declarações cujo código de erro está entre os pedidos.</summary>
    public IReadOnlyList<FieldError> RecusasDasDeclaracoes(IReadOnlySet<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        return [.. RecusasDasDeclaracoes().Where(recusa => codigos.Contains(recusa.Error.Code))];
    }

    public IReadOnlyList<FieldError> Conferir() => ConjuntoDasEtapas.Recusas(Etapas);

    /// <summary>A etapa não tem fase concluinte: o preliminar de um tipo de ato exige o definitivo do mesmo tipo dentro dela.</summary>
    public IReadOnlyList<FieldError> ConferirFechamentoDoCiclo() =>
    [
        .. Etapas.SelectMany(static (etapa, posicao) =>
            etapa.Atos.TiposComCicloAberto().Select(tipo => NaEtapa(posicao, "produtos", PreliminarSemDefinitivo(etapa.Nome, tipo)))),
    ];

    /// <summary>Toda etapa publicada declara a fase em que acontece; uma causa por etapa sem fase.</summary>
    public IReadOnlyList<FieldError> ConferirCompletude() =>
    [
        .. Etapas
            .Select(static (etapa, posicao) => (etapa, posicao))
            .Where(static par => par.etapa.FaseCodigo is null)
            .Select(static par => NaEtapa(par.posicao, "faseCodigo", EtapaSemFaseDeclarada(par.etapa.Nome))),
    ];

    public ItemConformidade ItemDaAncoraDoRecurso() => new(
        "cronograma_ancora_do_recurso_da_etapa",
        DimensaoConformidade.Cronograma,
        "Cronograma: cada janela de recurso da etapa ancora num produto preliminar da própria etapa",
        RecusasDasDeclaracoes(JanelasRecursaisDaEtapa.CodigosDaAncora).Count == 0);

    public IReadOnlyList<ItemConformidade> ItensDaEscala(CitacoesDeNota citacoes) => ConformidadeDaAvaliacaoDasEtapas.ItensDaEscala(Etapas, citacoes);

    public IReadOnlyList<ItemConformidade> ItensDaAvaliacao(CitacoesDeNota citacoes) => ConformidadeDaAvaliacaoDasEtapas.ItensDaAvaliacao(Etapas, citacoes);

    internal static DomainError PreliminarSemDefinitivo(string nomeDaEtapa, string tipo) => new(
        ProcessoSeletivoErrorCodes.EtapaComPreliminarSemDefinitivo,
        $"A etapa '{nomeDaEtapa}' publica o resultado preliminar do ato '{tipo}' e não publica o definitivo do mesmo tipo — a etapa não tem fase concluinte, e o ciclo dela fecha dentro dela.");

    internal static DomainError EtapaSemFaseDeclarada(string nomeDaEtapa) => new(
        "ProcessoSeletivo.EtapaSemFaseDeclarada",
        $"A etapa '{nomeDaEtapa}' não declara a fase em que acontece — declare a fase antes de publicar ou retificar o processo.");

    /// <summary>Localiza a recusa na etapa da posição; sem campo, o lugar é a própria etapa.</summary>
    internal static FieldError NaEtapa(int posicao, string? campo, DomainError erro) =>
        new(string.IsNullOrEmpty(campo) ? $"etapas[{posicao}]" : $"etapas[{posicao}].{campo}", erro);
}
