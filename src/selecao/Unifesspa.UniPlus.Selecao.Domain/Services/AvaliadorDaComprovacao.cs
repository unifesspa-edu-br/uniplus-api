namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Avalia as regras de comprovação e suas consequências para um candidato.</summary>
public sealed class AvaliadorDaComprovacao
{
    private readonly IReadOnlyDictionary<string, FatoResolvido> _fatos;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> _apresentacoes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> _ocorrenciasPorGrupo;
    private readonly IReadOnlySet<Guid> _exigenciasQueAguardamDados;
    private readonly Dictionary<Guid, SituacaoDaComprovacao> _situacoesPorRegra = [];
    private readonly Dictionary<Guid, StatusResolucaoExigencia> _situacoesPorExigencia = [];
    private readonly Dictionary<Guid, List<OcorrenciaAvaliada>> _resolvidas = [];
    private readonly HashSet<Guid> _regrasComConsequenciaAdiada = [];
    private readonly List<ConsequenciaAplicavel> _consequencias = [];
    private readonly List<PendenciaDeOrientacao> _pendencias = [];

    private AvaliadorDaComprovacao(
        IReadOnlyDictionary<string, FatoResolvido> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias, IReadOnlySet<Guid> exigenciasQueAguardamDados)
    {
        _fatos = fatos;
        _apresentacoes = apresentacoes;
        _ocorrenciasPorGrupo = ocorrencias;
        _exigenciasQueAguardamDados = exigenciasQueAguardamDados;
    }

    public static Result<ResultadoDaComprovacao> Resolver(
        RegrasDeComprovacaoDaVersao regras, IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoesPorExigenciaId,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>>? ocorrenciasPorGrupo = null, IReadOnlySet<Guid>? exigenciasQueAguardamDados = null)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(fatosResolvidos);
        ArgumentNullException.ThrowIfNull(apresentacoesPorExigenciaId);
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = ocorrenciasPorGrupo
            ?? new Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>>(StringComparer.Ordinal);
        DomainError? recusa = RecusaDasRegras(regras) ?? RecusaDasOcorrencias(ocorrencias);
        return recusa is not null ? Result<ResultadoDaComprovacao>.Failure(recusa)
            : Result<ResultadoDaComprovacao>.Success(new AvaliadorDaComprovacao(
                fatosResolvidos, apresentacoesPorExigenciaId, ocorrencias, exigenciasQueAguardamDados ?? new HashSet<Guid>()).Avaliar(regras!));
    }

    private static DomainError? RecusaDasRegras(RegrasDeComprovacaoDaVersao regras)
    {
        List<RegraDeComprovacao> regrasDeDocumento = [.. regras.RegrasSemGrupo.SelectMany(static r => r.EnumerarRegraEItens()).Where(static regra => regra.Forma == FormaDeComprovacao.Documento)];
        return RecusaDasRegrasDeDocumento(regrasDeDocumento) ?? (regras.RegrasSemGrupo.Any(ContemRepeticaoDentroDeRepeticao)
            ? new DomainError("RegraDeComprovacao.RepeticaoDentroDeRepeticao", "Uma regra repetida não pode conter outra regra de repetição.") : null);
    }

    private static DomainError? RecusaDasRegrasDeDocumento(List<RegraDeComprovacao> regrasDeDocumento)
    {
        if (regrasDeDocumento.Any(static regra => regra.DocumentoExigidoId is null || regra.DocumentoExigido is null))
        {
            return new DomainError("AvaliadorDaComprovacao.RegraSemDocumento", "Uma regra de documento não indica a exigência documental.");
        }
        return regrasDeDocumento.Select(static regra => regra.DocumentoExigidoId).Distinct().Count() != regrasDeDocumento.Count
            ? DocumentoReferenciadoMaisDeUmaVez("Uma exigência documental aparece em mais de uma regra de comprovação.") : null;
    }

    private static DomainError DocumentoReferenciadoMaisDeUmaVez(string mensagem) => new("AvaliadorDaComprovacao.DocumentoReferenciadoMaisDeUmaVez", mensagem);

    private static DomainError? RecusaDasOcorrencias(IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrenciasPorGrupo)
    {
        foreach (IReadOnlyList<OcorrenciaDoGrupo> ocorrencias in ocorrenciasPorGrupo.Values)
        {
            if (ocorrencias.Any(static i => string.IsNullOrWhiteSpace(i.OcorrenciaId)))
            {
                return new("AvaliadorDaComprovacao.OcorrenciaDoGrupoInvalida", "Uma ocorrência do grupo não tem identificador.");
            }
            if (ocorrencias.Select(static i => i.OcorrenciaId).Distinct(StringComparer.Ordinal).Count() != ocorrencias.Count)
            {
                return new("AvaliadorDaComprovacao.OcorrenciaDoGrupoInvalida", "Duas ou mais ocorrências do grupo têm o mesmo identificador.");
            }
        }
        return null;
    }

    private ResultadoDaComprovacao Avaliar(RegrasDeComprovacaoDaVersao regras)
    {
        foreach (RegraDeComprovacao regraSemGrupo in regras.RegrasSemGrupo)
        {
            SituacaoDaComprovacao situacao = ResolverRegra(regraSemGrupo);
            IdentificarConsequenciasQueAguardamDados(regraSemGrupo, _situacoesPorRegra, _exigenciasQueAguardamDados, _regrasComConsequenciaAdiada);
            DeterminarConsequenciasAplicaveis(regraSemGrupo, situacao);
        }
        return new(_situacoesPorRegra, _situacoesPorExigencia, _consequencias, _pendencias, SituacoesDeExigenciaPorOcorrencia(), SituacoesDeRegraPorOcorrencia(), _regrasComConsequenciaAdiada);
    }

    private List<SituacaoDaExigenciaPorOcorrencia> SituacoesDeExigenciaPorOcorrencia() => [.. _resolvidas.Values.SelectMany(static r => r)
        .SelectMany(static i => i.SituacoesDasExigencias.Select(par => new SituacaoDaExigenciaPorOcorrencia(par.Key, i.OcorrenciaId, par.Value)))];

    private List<SituacaoDaRegraPorOcorrencia> SituacoesDeRegraPorOcorrencia() => [.. _resolvidas.Values.SelectMany(static r => r)
        .SelectMany(static i => i.SituacoesDasRegras.Select(par => new SituacaoDaRegraPorOcorrencia(par.Key, i.OcorrenciaId, par.Value)))];

    private SituacaoDaComprovacao ResolverRegra(RegraDeComprovacao regra)
    {
        SituacaoDaComprovacao situacao = regra.GrupoDoFormularioParaRepeticao is { } grupo ? ResolverRegraRepetida(regra, grupo) : ResolverRegraSemRepeticao(regra);
        _situacoesPorRegra[regra.Id] = situacao;
        return situacao;
    }

    private SituacaoDaComprovacao ResolverRegraSemRepeticao(RegraDeComprovacao regra) => regra.Forma == FormaDeComprovacao.Documento ? ResolverRegraDeDocumento(regra) : ResolverGrupo(regra);

    private SituacaoDaComprovacao ResolverRegraRepetida(RegraDeComprovacao regra, string grupo)
    {
        List<OcorrenciaAvaliada> resolvidas = [.. _ocorrenciasPorGrupo.GetValueOrDefault(grupo, []).Select(i => AvaliarOcorrencia(regra, i))];
        _resolvidas[regra.Id] = resolvidas;
        return ResolverTodosOsItens([.. resolvidas.Select(static r => r.Situacao)]);
    }

    private OcorrenciaAvaliada AvaliarOcorrencia(RegraDeComprovacao regra, OcorrenciaDoGrupo ocorrencia)
    {
        AvaliadorDaComprovacao resolvedor = new(MesclarFatosDaOcorrencia(_fatos, ocorrencia.Atributos),
            FiltrarApresentacoesDaOcorrencia(_apresentacoes, ocorrencia.OcorrenciaId), _ocorrenciasPorGrupo, _exigenciasQueAguardamDados);
        SituacaoDaComprovacao situacao = resolvedor.ResolverRegraSemRepeticao(regra);
        resolvedor._situacoesPorRegra[regra.Id] = situacao;
        IdentificarConsequenciasQueAguardamDados(regra, resolvedor._situacoesPorRegra, _exigenciasQueAguardamDados, resolvedor._regrasComConsequenciaAdiada, naOcorrencia: true);
        return new(ocorrencia.OcorrenciaId, situacao, resolvedor._situacoesPorRegra, resolvedor._situacoesPorExigencia, resolvedor._regrasComConsequenciaAdiada);
    }

    private static Dictionary<string, FatoResolvido> MesclarFatosDaOcorrencia(
        IReadOnlyDictionary<string, FatoResolvido> fatos, IReadOnlyDictionary<string, FatoResolvido> atributos)
    {
        Dictionary<string, FatoResolvido> mesclados = new(fatos, StringComparer.Ordinal);
        foreach (KeyValuePair<string, FatoResolvido> atributo in atributos)
        {
            mesclados[atributo.Key] = atributo.Value;
        }
        return mesclados;
    }

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> FiltrarApresentacoesDaOcorrencia(
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes, string ocorrenciaId) => apresentacoes
        .Select(par => (par.Key, DaOcorrencia: (IReadOnlyList<ApresentacaoDocumento>)par.Value.Where(a => a.OcorrenciaId == ocorrenciaId).ToList()))
        .Where(static par => par.DaOcorrencia.Count > 0).ToDictionary(static par => par.Key, static par => par.DaOcorrencia);

    private SituacaoDaComprovacao ResolverRegraDeDocumento(RegraDeComprovacao regra)
    {
        DocumentoExigido documento = regra.DocumentoExigido!;
        Ternario aplicavel = _exigenciasQueAguardamDados.Contains(documento.Id) ? Ternario.Indeterminado : documento.AplicavelPara(_fatos);
        if (aplicavel != Ternario.Verdadeiro)
        {
            return RegistrarInaplicabilidade(documento.Id, aplicavel);
        }
        int quantidade = regra.QuantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao;
        List<ApresentacaoDocumento> distintas = [.. _apresentacoes.GetValueOrDefault(documento.Id, []).DistinctBy(static a => a.Id)];
        bool cumprida = regra.Distincao?.AtendidaPor(distintas, quantidade) ?? distintas.Count >= quantidade;
        _situacoesPorExigencia[documento.Id] = cumprida ? StatusResolucaoExigencia.Cumprida : StatusResolucaoExigencia.PendenteDeComprovacao;
        return cumprida ? SituacaoDaComprovacao.Cumprida : SituacaoDaComprovacao.PendenteDeComprovacao;
    }

    private SituacaoDaComprovacao RegistrarInaplicabilidade(Guid documentoId, Ternario aplicavel)
    {
        _situacoesPorExigencia[documentoId] = aplicavel == Ternario.Indeterminado
            ? StatusResolucaoExigencia.AguardandoDados : StatusResolucaoExigencia.NaoExigida;
        return aplicavel == Ternario.Indeterminado ? SituacaoDaComprovacao.AguardandoDados : SituacaoDaComprovacao.NaoExigida;
    }

    private SituacaoDaComprovacao ResolverGrupo(RegraDeComprovacao regra)
    {
        List<SituacaoDaComprovacao> itens = [.. regra.Itens.Select(ResolverRegra)];
        return regra.Forma == FormaDeComprovacao.TodosOsItens ? ResolverTodosOsItens(itens) : ResolverQuantidadeMinima(itens, regra.QuantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao);
    }

    private static SituacaoDaComprovacao ResolverTodosOsItens(IReadOnlyList<SituacaoDaComprovacao> itens)
    {
        List<SituacaoDaComprovacao> restantes = [.. itens.Where(static e => e != SituacaoDaComprovacao.NaoExigida)];
        if (restantes.Count == 0)
        {
            return SituacaoDaComprovacao.NaoExigida;
        }
        if (restantes.Contains(SituacaoDaComprovacao.SemPossibilidadeDeCumprimento))
        {
            return SituacaoDaComprovacao.SemPossibilidadeDeCumprimento;
        }
        if (restantes.All(static e => e == SituacaoDaComprovacao.Cumprida))
        {
            return SituacaoDaComprovacao.Cumprida;
        }
        return restantes.Contains(SituacaoDaComprovacao.PendenteDeComprovacao) ? SituacaoDaComprovacao.PendenteDeComprovacao : SituacaoDaComprovacao.AguardandoDados;
    }

    private static SituacaoDaComprovacao ResolverQuantidadeMinima(IReadOnlyList<SituacaoDaComprovacao> itens, int quantidadeMinima)
    {
        if (itens.All(static e => e == SituacaoDaComprovacao.NaoExigida))
        {
            return SituacaoDaComprovacao.NaoExigida;
        }
        int cumpridos = itens.Count(static e => e == SituacaoDaComprovacao.Cumprida);
        if (cumpridos >= quantidadeMinima)
        {
            return SituacaoDaComprovacao.Cumprida;
        }
        int pendentes = itens.Count(static e => e == SituacaoDaComprovacao.PendenteDeComprovacao);
        int aguardandoDados = itens.Count(static e => e == SituacaoDaComprovacao.AguardandoDados);
        return cumpridos + pendentes + aguardandoDados < quantidadeMinima ? SituacaoDaComprovacao.SemPossibilidadeDeCumprimento
            : aguardandoDados > 0 ? SituacaoDaComprovacao.AguardandoDados : SituacaoDaComprovacao.PendenteDeComprovacao;
    }

    private static bool IdentificarConsequenciasQueAguardamDados(RegraDeComprovacao regra, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> situacoesPorRegra,
        IReadOnlySet<Guid> exigenciasQueAguardamDados, HashSet<Guid> regrasComConsequenciaAdiada, bool naOcorrencia = false)
    {
        if (regra.GrupoDoFormularioParaRepeticao is not null && !naOcorrencia)
        {
            return false;
        }
        SituacaoDaComprovacao situacao = situacoesPorRegra[regra.Id];
        bool aguardaDados = regra.Forma == FormaDeComprovacao.Documento ? RequerAvaliacaoDeConsequencia(situacao) && exigenciasQueAguardamDados.Contains(regra.DocumentoExigido!.Id)
            : GrupoAguardaDadosParaConsequencia(regra, situacoesPorRegra, exigenciasQueAguardamDados, regrasComConsequenciaAdiada, naOcorrencia);
        if (aguardaDados)
        {
            regrasComConsequenciaAdiada.Add(regra.Id);
        }
        return aguardaDados;
    }

    private static bool GrupoAguardaDadosParaConsequencia(RegraDeComprovacao regra, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> situacoesPorRegra,
        IReadOnlySet<Guid> exigenciasQueAguardamDados, HashSet<Guid> regrasComConsequenciaAdiada, bool naOcorrencia)
    {
        List<bool> decisivos = [];
        foreach (RegraDeComprovacao item in regra.Itens)
        {
            bool aguardaDados = IdentificarConsequenciasQueAguardamDados(item, situacoesPorRegra, exigenciasQueAguardamDados, regrasComConsequenciaAdiada, naOcorrencia);
            if (RequerAvaliacaoDeConsequencia(situacoesPorRegra[item.Id]))
            {
                decisivos.Add(aguardaDados);
            }
        }
        return situacoesPorRegra[regra.Id] is SituacaoDaComprovacao.PendenteDeComprovacao or SituacaoDaComprovacao.AguardandoDados && decisivos.Count > 0 && decisivos.All(static b => b);
    }

    private void DeterminarConsequenciasAplicaveis(RegraDeComprovacao regra, SituacaoDaComprovacao situacao)
    {
        if (!RequerAvaliacaoDeConsequencia(situacao) || _regrasComConsequenciaAdiada.Contains(regra.Id))
        {
            return;
        }
        if (regra.GrupoDoFormularioParaRepeticao is not null)
        {
            foreach (OcorrenciaAvaliada ocorrencia in _resolvidas.GetValueOrDefault(regra.Id, []))
            {
                DeterminarConsequenciasDaOcorrencia(regra, ocorrencia);
            }
            return;
        }
        DeterminarConsequenciasAplicaveisParaRegra(regra, _situacoesPorRegra, _regrasComConsequenciaAdiada, _consequencias, _pendencias);
    }

    private void DeterminarConsequenciasDaOcorrencia(RegraDeComprovacao regra, OcorrenciaAvaliada ocorrencia)
    {
        if (!RequerAvaliacaoDeConsequencia(ocorrencia.Situacao) || ocorrencia.RegrasComConsequenciaAdiada.Contains(regra.Id))
        {
            return;
        }
        List<ConsequenciaAplicavel> consequencias = [];
        List<PendenciaDeOrientacao> pendencias = [];
        DeterminarConsequenciasAplicaveisParaRegra(regra, ocorrencia.SituacoesDasRegras, ocorrencia.RegrasComConsequenciaAdiada, consequencias, pendencias);
        _consequencias.AddRange(consequencias.Select(c => c with { OcorrenciaId = ocorrencia.OcorrenciaId }));
        _pendencias.AddRange(pendencias.Select(p => p with { OcorrenciaId = ocorrencia.OcorrenciaId }));
    }

    private void DeterminarConsequenciasAplicaveisParaRegra(RegraDeComprovacao regra, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> situacoesPorRegra,
        IReadOnlySet<Guid> regrasComConsequenciaAdiada, List<ConsequenciaAplicavel> consequencias, List<PendenciaDeOrientacao> pendencias)
    {
        switch (regra.Forma)
        {
            case FormaDeComprovacao.Documento:
                DeterminarConsequenciaDoDocumento(regra, consequencias);
                break;
            case FormaDeComprovacao.QuantidadeMinimaDeItens:
                DeterminarConsequenciaDoGrupo(regra, situacoesPorRegra, regrasComConsequenciaAdiada, consequencias, pendencias);
                break;
            case FormaDeComprovacao.TodosOsItens:
                DeterminarConsequenciasDosItensObrigatorios(regra, situacoesPorRegra, regrasComConsequenciaAdiada, consequencias, pendencias);
                break;
        }
    }

    private static void DeterminarConsequenciaDoDocumento(RegraDeComprovacao regra, List<ConsequenciaAplicavel> consequencias)
    {
        if (regra.DocumentoExigido!.ConsequenciaIndeferimento is { } consequencia)
        {
            consequencias.Add(new(regra.Id, FormaDeComprovacao.Documento, consequencia));
        }
    }

    private void DeterminarConsequenciasDosItensObrigatorios(RegraDeComprovacao regra, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> situacoesPorRegra,
        IReadOnlySet<Guid> regrasComConsequenciaAdiada, List<ConsequenciaAplicavel> consequencias, List<PendenciaDeOrientacao> pendencias)
    {
        foreach (RegraDeComprovacao item in regra.Itens.Where(f => RequerAvaliacaoDeConsequencia(situacoesPorRegra[f.Id])))
        {
            DeterminarConsequenciasDoItem(item, situacoesPorRegra, regrasComConsequenciaAdiada, consequencias, pendencias);
        }
    }

    private static void DeterminarConsequenciaDoGrupo(RegraDeComprovacao regra, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> situacoesPorRegra,
        IReadOnlySet<Guid> regrasComConsequenciaAdiada, List<ConsequenciaAplicavel> consequencias, List<PendenciaDeOrientacao> pendencias)
    {
        if (regra.Consequencia is { } consequencia)
        {
            consequencias.Add(new(regra.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, consequencia));
        }
        foreach (RegraDeComprovacao item in regra.Itens.Where(f => RequerAvaliacaoDeConsequencia(situacoesPorRegra[f.Id]) && !regrasComConsequenciaAdiada.Contains(f.Id)))
        {
            pendencias.Add(new(item.Id));
        }
    }

    private void DeterminarConsequenciasDoItem(RegraDeComprovacao item, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> situacoesPorRegra,
        IReadOnlySet<Guid> regrasComConsequenciaAdiada, List<ConsequenciaAplicavel> consequencias, List<PendenciaDeOrientacao> pendencias)
    {
        if (regrasComConsequenciaAdiada.Contains(item.Id))
        {
            return;
        }
        if (item.GrupoDoFormularioParaRepeticao is not null)
        {
            DeterminarConsequenciasAplicaveis(item, situacoesPorRegra[item.Id]);
        }
        else
        {
            DeterminarConsequenciasAplicaveisParaRegra(item, situacoesPorRegra, regrasComConsequenciaAdiada, consequencias, pendencias);
        }
    }

    private static bool RequerAvaliacaoDeConsequencia(SituacaoDaComprovacao situacao) => situacao is SituacaoDaComprovacao.PendenteDeComprovacao or SituacaoDaComprovacao.AguardandoDados or SituacaoDaComprovacao.SemPossibilidadeDeCumprimento;

    private static bool ContemRepeticaoDentroDeRepeticao(RegraDeComprovacao regra) =>
        (regra.GrupoDoFormularioParaRepeticao is not null && regra.Itens.Any(ContemRepeticaoNaRegraOuNosItens)) || regra.Itens.Any(ContemRepeticaoDentroDeRepeticao);

    private static bool ContemRepeticaoNaRegraOuNosItens(RegraDeComprovacao regra) => regra.GrupoDoFormularioParaRepeticao is not null || regra.Itens.Any(ContemRepeticaoNaRegraOuNosItens);

    private sealed record OcorrenciaAvaliada(string OcorrenciaId, SituacaoDaComprovacao Situacao,
        Dictionary<Guid, SituacaoDaComprovacao> SituacoesDasRegras, Dictionary<Guid, StatusResolucaoExigencia> SituacoesDasExigencias, IReadOnlySet<Guid> RegrasComConsequenciaAdiada);
}
