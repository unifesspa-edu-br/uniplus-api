namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Resolve a satisfação e as consequências da árvore congelada para um candidato.</summary>
public sealed class AvaliadorDaComprovacao
{
    private readonly IReadOnlyDictionary<string, FatoResolvido> _fatos;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> _apresentacoes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> _instancias;
    private readonly IReadOnlySet<Guid> _bloqueadas;
    private readonly Dictionary<Guid, SituacaoDaComprovacao> _estados = [];
    private readonly Dictionary<Guid, StatusResolucaoExigencia> _status = [];
    private readonly Dictionary<Guid, List<InstanciaResolvida>> _resolvidas = [];
    private readonly HashSet<Guid> _suprimidos = [];
    private readonly List<ConsequenciaEmitida> _consequencias = [];
    private readonly List<PendenciaDeOrientacao> _pendencias = [];

    private AvaliadorDaComprovacao(
        IReadOnlyDictionary<string, FatoResolvido> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> instancias, IReadOnlySet<Guid> bloqueadas)
    {
        _fatos = fatos;
        _apresentacoes = apresentacoes;
        _instancias = instancias;
        _bloqueadas = bloqueadas;
    }

    public static Result<ResultadoDaComprovacao> Resolver(
        RegrasDeComprovacaoDaVersao arvore, IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoesPorExigenciaId,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>>? instanciasPorGrupo = null, IReadOnlySet<Guid>? exigenciasBloqueadas = null)
    {
        ArgumentNullException.ThrowIfNull(arvore);
        ArgumentNullException.ThrowIfNull(fatosResolvidos);
        ArgumentNullException.ThrowIfNull(apresentacoesPorExigenciaId);
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> instancias = instanciasPorGrupo
            ?? new Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>>(StringComparer.Ordinal);
        DomainError? recusa = RecusaDaArvore(arvore) ?? RecusaDasInstancias(instancias);
        return recusa is not null ? Result<ResultadoDaComprovacao>.Failure(recusa)
            : Result<ResultadoDaComprovacao>.Success(new AvaliadorDaComprovacao(
                fatosResolvidos, apresentacoesPorExigenciaId, instancias, exigenciasBloqueadas ?? new HashSet<Guid>()).Avaliar(arvore!));
    }

    private static DomainError? RecusaDaArvore(RegrasDeComprovacaoDaVersao arvore)
    {
        List<NoExigencia> folhas = [.. arvore.Raizes.SelectMany(static r => r.AchatarComDescendentes()).Where(static n => n.Tipo == TipoNo.Folha)];
        return RecusaDasFolhas(folhas) ?? (arvore.Raizes.Any(ContemRepeticaoAninhada)
            ? EstruturaInvalida("A árvore congelada tem repetição por entidade aninhada — uma subárvore repetePorEntidade não pode conter outra.") : null);
    }

    private static DomainError? RecusaDasFolhas(List<NoExigencia> folhas)
    {
        if (folhas.Any(static n => n.DocumentoExigidoId is null || n.DocumentoExigido is null))
        {
            return EstruturaInvalida("A árvore congelada tem folha sem DocumentoExigido — toda folha precisa envolver uma exigência.");
        }
        return folhas.Select(static n => n.DocumentoExigidoId).Distinct().Count() != folhas.Count
            ? EstruturaInvalida("A árvore congelada tem exigenciaId repetido entre folhas — cada exigência precisa de identidade única.") : null;
    }

    private static DomainError EstruturaInvalida(string mensagem) => new("AvaliadorDaComprovacao.ArvoreEstruturalmenteInvalida", mensagem);

    private static DomainError? RecusaDasInstancias(IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> instancias)
    {
        foreach (IReadOnlyList<OcorrenciaDoGrupo> ocorrencias in instancias.Values)
        {
            if (ocorrencias.Any(static i => string.IsNullOrWhiteSpace(i.EntidadeId)))
            {
                return new("AvaliadorDaComprovacao.InstanciaEntidadeInvalida", "Uma ocorrência do grupo tem EntidadeId vazio ou em branco.");
            }
            if (ocorrencias.Select(static i => i.EntidadeId).Distinct(StringComparer.Ordinal).Count() != ocorrencias.Count)
            {
                return new("AvaliadorDaComprovacao.InstanciaEntidadeInvalida", "Duas ou mais ocorrências do grupo declaram o mesmo EntidadeId — cada ocorrência precisa de identidade única.");
            }
        }
        return null;
    }

    private ResultadoDaComprovacao Avaliar(RegrasDeComprovacaoDaVersao arvore)
    {
        foreach (NoExigencia raiz in arvore.Raizes)
        {
            SituacaoDaComprovacao estado = ResolverNo(raiz);
            IdentificarConsequenciasQueAguardamDados(raiz, _estados, _bloqueadas, _suprimidos);
            DeterminarConsequenciasAplicaveis(raiz, estado);
        }
        return new(_estados, _status, _consequencias, _pendencias, StatusPorEntidade(), EstadosPorEntidade(), _suprimidos);
    }

    private List<StatusPorEntidade> StatusPorEntidade() => [.. _resolvidas.Values.SelectMany(static r => r)
        .SelectMany(static i => i.StatusDescendentes.Select(par => new StatusPorEntidade(par.Key, i.EntidadeId, par.Value)))];

    private List<EstadoPorEntidade> EstadosPorEntidade() => [.. _resolvidas.Values.SelectMany(static r => r)
        .SelectMany(static i => i.EstadosDescendentes.Select(par => new EstadoPorEntidade(par.Key, i.EntidadeId, par.Value)))];

    private SituacaoDaComprovacao ResolverNo(NoExigencia no)
    {
        SituacaoDaComprovacao estado = no.RepetePorEntidade is { } grupo ? ResolverNoRepetido(no, grupo) : ResolverNoSemRepeticao(no);
        _estados[no.Id] = estado;
        return estado;
    }

    private SituacaoDaComprovacao ResolverNoSemRepeticao(NoExigencia no) => no.Tipo == TipoNo.Folha ? ResolverFolha(no) : ResolverGrupo(no);

    private SituacaoDaComprovacao ResolverNoRepetido(NoExigencia no, string grupo)
    {
        List<InstanciaResolvida> resolvidas = [.. _instancias.GetValueOrDefault(grupo, []).Select(i => ResolverInstancia(no, i))];
        _resolvidas[no.Id] = resolvidas;
        return ResolverE([.. resolvidas.Select(static r => r.Estado)]);
    }

    private InstanciaResolvida ResolverInstancia(NoExigencia no, OcorrenciaDoGrupo instancia)
    {
        AvaliadorDaComprovacao resolvedor = new(MesclarFatosDeEntidade(_fatos, instancia.Atributos),
            FiltrarApresentacoesPorEntidade(_apresentacoes, instancia.EntidadeId), _instancias, _bloqueadas);
        SituacaoDaComprovacao estado = resolvedor.ResolverNoSemRepeticao(no);
        resolvedor._estados[no.Id] = estado;
        IdentificarConsequenciasQueAguardamDados(no, resolvedor._estados, _bloqueadas, resolvedor._suprimidos, naInstancia: true);
        return new(instancia.EntidadeId, estado, resolvedor._estados, resolvedor._status, resolvedor._suprimidos);
    }

    private static Dictionary<string, FatoResolvido> MesclarFatosDeEntidade(
        IReadOnlyDictionary<string, FatoResolvido> fatos, IReadOnlyDictionary<string, FatoResolvido> atributos)
    {
        Dictionary<string, FatoResolvido> mesclados = new(fatos, StringComparer.Ordinal);
        foreach (KeyValuePair<string, FatoResolvido> atributo in atributos)
        {
            mesclados[atributo.Key] = atributo.Value;
        }
        return mesclados;
    }

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> FiltrarApresentacoesPorEntidade(
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes, string entidadeId) => apresentacoes
        .Select(par => (par.Key, DaEntidade: (IReadOnlyList<ApresentacaoDocumento>)par.Value.Where(a => a.EntidadeId == entidadeId).ToList()))
        .Where(static par => par.DaEntidade.Count > 0).ToDictionary(static par => par.Key, static par => par.DaEntidade);

    private SituacaoDaComprovacao ResolverFolha(NoExigencia no)
    {
        DocumentoExigido documento = no.DocumentoExigido!;
        Ternario aplicavel = _bloqueadas.Contains(documento.Id) ? Ternario.Indeterminado : documento.AplicavelPara(_fatos);
        if (aplicavel != Ternario.Verdadeiro)
        {
            return RegistrarInaplicabilidade(documento.Id, aplicavel);
        }
        int quantidade = no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao;
        List<ApresentacaoDocumento> distintas = [.. _apresentacoes.GetValueOrDefault(documento.Id, []).DistinctBy(static a => a.Id)];
        bool satisfeita = no.Distincao?.AtendidaPor(distintas, quantidade) ?? distintas.Count >= quantidade;
        _status[documento.Id] = satisfeita ? StatusResolucaoExigencia.Satisfeita : StatusResolucaoExigencia.Pendente;
        return satisfeita ? SituacaoDaComprovacao.Satisfeito : SituacaoDaComprovacao.Pendente;
    }

    private SituacaoDaComprovacao RegistrarInaplicabilidade(Guid documentoId, Ternario aplicavel)
    {
        _status[documentoId] = aplicavel == Ternario.Indeterminado
            ? StatusResolucaoExigencia.AplicabilidadeIndeterminada : StatusResolucaoExigencia.NaoAplicavel;
        return aplicavel == Ternario.Indeterminado ? SituacaoDaComprovacao.Indeterminado : SituacaoDaComprovacao.NaoAplicavel;
    }

    private SituacaoDaComprovacao ResolverGrupo(NoExigencia no)
    {
        List<SituacaoDaComprovacao> filhos = [.. no.Filhos.Select(ResolverNo)];
        return no.Tipo == TipoNo.GrupoE ? ResolverE(filhos) : ResolverOu(filhos, no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao);
    }

    private static SituacaoDaComprovacao ResolverE(IReadOnlyList<SituacaoDaComprovacao> filhos)
    {
        List<SituacaoDaComprovacao> restantes = [.. filhos.Where(static e => e != SituacaoDaComprovacao.NaoAplicavel)];
        if (restantes.Count == 0)
        {
            return SituacaoDaComprovacao.NaoAplicavel;
        }
        if (restantes.Contains(SituacaoDaComprovacao.Impossivel))
        {
            return SituacaoDaComprovacao.Impossivel;
        }
        if (restantes.All(static e => e == SituacaoDaComprovacao.Satisfeito))
        {
            return SituacaoDaComprovacao.Satisfeito;
        }
        return restantes.Contains(SituacaoDaComprovacao.Pendente) ? SituacaoDaComprovacao.Pendente : SituacaoDaComprovacao.Indeterminado;
    }

    private static SituacaoDaComprovacao ResolverOu(IReadOnlyList<SituacaoDaComprovacao> filhos, int quantidadeMinima)
    {
        if (filhos.All(static e => e == SituacaoDaComprovacao.NaoAplicavel))
        {
            return SituacaoDaComprovacao.NaoAplicavel;
        }
        int satisfeitos = filhos.Count(static e => e == SituacaoDaComprovacao.Satisfeito);
        if (satisfeitos >= quantidadeMinima)
        {
            return SituacaoDaComprovacao.Satisfeito;
        }
        int pendentes = filhos.Count(static e => e == SituacaoDaComprovacao.Pendente);
        int indeterminados = filhos.Count(static e => e == SituacaoDaComprovacao.Indeterminado);
        return satisfeitos + pendentes + indeterminados < quantidadeMinima ? SituacaoDaComprovacao.Impossivel
            : indeterminados > 0 ? SituacaoDaComprovacao.Indeterminado : SituacaoDaComprovacao.Pendente;
    }

    private static bool IdentificarConsequenciasQueAguardamDados(NoExigencia no, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> estados,
        IReadOnlySet<Guid> bloqueadas, HashSet<Guid> suprimidos, bool naInstancia = false)
    {
        if (no.RepetePorEntidade is not null && !naInstancia)
        {
            return false;
        }
        SituacaoDaComprovacao estado = estados[no.Id];
        bool bloqueado = no.Tipo == TipoNo.Folha ? EhPendenteOuPior(estado) && bloqueadas.Contains(no.DocumentoExigido!.Id)
            : GrupoComEmissaoBloqueada(no, estados, bloqueadas, suprimidos, naInstancia);
        if (bloqueado)
        {
            suprimidos.Add(no.Id);
        }
        return bloqueado;
    }

    private static bool GrupoComEmissaoBloqueada(NoExigencia no, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> estados,
        IReadOnlySet<Guid> bloqueadas, HashSet<Guid> suprimidos, bool naInstancia)
    {
        List<bool> decisivos = [];
        foreach (NoExigencia filho in no.Filhos)
        {
            bool bloqueado = IdentificarConsequenciasQueAguardamDados(filho, estados, bloqueadas, suprimidos, naInstancia);
            if (EhPendenteOuPior(estados[filho.Id]))
            {
                decisivos.Add(bloqueado);
            }
        }
        return estados[no.Id] is SituacaoDaComprovacao.Pendente or SituacaoDaComprovacao.Indeterminado && decisivos.Count > 0 && decisivos.All(static b => b);
    }

    private void DeterminarConsequenciasAplicaveis(NoExigencia no, SituacaoDaComprovacao estado)
    {
        if (!EhPendenteOuPior(estado) || _suprimidos.Contains(no.Id))
        {
            return;
        }
        if (no.RepetePorEntidade is not null)
        {
            foreach (InstanciaResolvida instancia in _resolvidas.GetValueOrDefault(no.Id, []))
            {
                EmitirInstancia(no, instancia);
            }
            return;
        }
        DeterminarConsequenciasAplicaveisParaRegra(no, _estados, _suprimidos, _consequencias, _pendencias);
    }

    private void EmitirInstancia(NoExigencia no, InstanciaResolvida instancia)
    {
        if (!EhPendenteOuPior(instancia.Estado) || instancia.RegrasComConsequenciaAdiada.Contains(no.Id))
        {
            return;
        }
        List<ConsequenciaEmitida> consequencias = [];
        List<PendenciaDeOrientacao> pendencias = [];
        DeterminarConsequenciasAplicaveisParaRegra(no, instancia.EstadosDescendentes, instancia.RegrasComConsequenciaAdiada, consequencias, pendencias);
        _consequencias.AddRange(consequencias.Select(c => c with { EntidadeId = instancia.EntidadeId }));
        _pendencias.AddRange(pendencias.Select(p => p with { EntidadeId = instancia.EntidadeId }));
    }

    private void DeterminarConsequenciasAplicaveisParaRegra(NoExigencia no, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> estados,
        IReadOnlySet<Guid> suprimidos, List<ConsequenciaEmitida> consequencias, List<PendenciaDeOrientacao> pendencias)
    {
        switch (no.Tipo)
        {
            case TipoNo.Folha:
                EmitirFolha(no, consequencias);
                break;
            case TipoNo.GrupoOu:
                EmitirGrupoOu(no, estados, suprimidos, consequencias, pendencias);
                break;
            case TipoNo.GrupoE:
                EmitirGrupoE(no, estados, suprimidos, consequencias, pendencias);
                break;
        }
    }

    private static void EmitirFolha(NoExigencia no, List<ConsequenciaEmitida> consequencias)
    {
        if (no.DocumentoExigido!.ConsequenciaIndeferimento is { } consequencia)
        {
            consequencias.Add(new(no.Id, TipoNo.Folha, consequencia));
        }
    }

    private void EmitirGrupoE(NoExigencia no, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> estados,
        IReadOnlySet<Guid> suprimidos, List<ConsequenciaEmitida> consequencias, List<PendenciaDeOrientacao> pendencias)
    {
        foreach (NoExigencia filho in no.Filhos.Where(f => EhPendenteOuPior(estados[f.Id])))
        {
            EmitirDescendente(filho, estados, suprimidos, consequencias, pendencias);
        }
    }

    private static void EmitirGrupoOu(NoExigencia no, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> estados,
        IReadOnlySet<Guid> suprimidos, List<ConsequenciaEmitida> consequencias, List<PendenciaDeOrientacao> pendencias)
    {
        if (no.Consequencia is { } consequencia)
        {
            consequencias.Add(new(no.Id, TipoNo.GrupoOu, consequencia));
        }
        foreach (NoExigencia filho in no.Filhos.Where(f => EhPendenteOuPior(estados[f.Id]) && !suprimidos.Contains(f.Id)))
        {
            pendencias.Add(new(filho.Id));
        }
    }

    private void EmitirDescendente(NoExigencia filho, IReadOnlyDictionary<Guid, SituacaoDaComprovacao> estados,
        IReadOnlySet<Guid> suprimidos, List<ConsequenciaEmitida> consequencias, List<PendenciaDeOrientacao> pendencias)
    {
        if (suprimidos.Contains(filho.Id))
        {
            return;
        }
        if (filho.RepetePorEntidade is not null)
        {
            DeterminarConsequenciasAplicaveis(filho, estados[filho.Id]);
        }
        else { DeterminarConsequenciasAplicaveisParaRegra(filho, estados, suprimidos, consequencias, pendencias); }
    }

    private static bool EhPendenteOuPior(SituacaoDaComprovacao estado) => estado is SituacaoDaComprovacao.Pendente or SituacaoDaComprovacao.Indeterminado or SituacaoDaComprovacao.Impossivel;

    private static bool ContemRepeticaoAninhada(NoExigencia no) =>
        (no.RepetePorEntidade is not null && no.Filhos.Any(ContemRepeticaoOuDescendente)) || no.Filhos.Any(ContemRepeticaoAninhada);

    private static bool ContemRepeticaoOuDescendente(NoExigencia no) => no.RepetePorEntidade is not null || no.Filhos.Any(ContemRepeticaoOuDescendente);

    private sealed record InstanciaResolvida(string EntidadeId, SituacaoDaComprovacao Estado,
        Dictionary<Guid, SituacaoDaComprovacao> EstadosDescendentes, Dictionary<Guid, StatusResolucaoExigencia> StatusDescendentes, IReadOnlySet<Guid> RegrasComConsequenciaAdiada);
}
