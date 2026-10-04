namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Abstractions;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Avalia a configuração viva do processo — rascunho ou sessão de retificação, nunca o envelope
/// publicado — contra um perfil simulado de candidato: o que cada formulário mostra e exige e os
/// documentos que a árvore de exigências pediria (UNI-REQ-0144, UNI-REQ-0145, UNI-REQ-0064).
/// </summary>
/// <remarks>
/// O perfil carrega fatos pessoais e sensíveis simulados: não é gravado nem registrado em log.
/// </remarks>
public sealed record PreVisualizarProcessoSeletivoQuery(Guid ProcessoSeletivoId, PreVisualizacaoDoProcessoInput Simulacao)
    : IQuery<Result<PreVisualizacaoDoProcessoDto>>;

/// <summary>
/// A pré-visualização usa os mesmos avaliadores da execução: o do formulário, com as derivações e
/// os agregados do processo intercalados, e o da árvore de exigências, sem apresentação nenhuma —
/// a folha aplicável sai pendente, isto é, exigida.
/// </summary>
public static class PreVisualizarProcessoSeletivoQueryHandler
{
    public const string OcorrenciaInvalida = "ProcessoSeletivo.OcorrenciaSimuladaInvalida";

    public static async Task<Result<PreVisualizacaoDoProcessoDto>> Handle(
        PreVisualizarProcessoSeletivoQuery query,
        IProcessoSeletivoRepository repository,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);

        ProcessoSeletivo? processo = await repository.ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        PreVisualizacaoDoProcessoInput simulacao = query.Simulacao ?? new(null, null, null, null);
        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias, List<FieldError> erros) = LerOcorrencias(simulacao.Grupos);

        IReadOnlyList<FatoCandidatoView> fatosDoCatalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, FatoCandidatoView> catalogo = fatosDoCatalogo.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

        // A oferta de valores é a mesma que a publicação congelaria; o campo que o catálogo deixou
        // de aceitar é recusado antes, como na publicação.
        if (ConferenciaDeColetabilidadeDeFatos.Conferir(processo, catalogo) is { IsFailure: true } naoColetavel)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(naoColetavel.Error!);
        }

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> oferta =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo);
        if (oferta.IsFailure)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(oferta.Error!);
        }

        Result<IReadOnlyList<RegrasDerivacaoFato>> derivacoes = Derivacoes(processo, catalogo);
        if (derivacoes.IsFailure)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(derivacoes.Error!);
        }

        if (erros.Count > 0)
        {
            return Result<PreVisualizacaoDoProcessoDto>.ValidationFailure(erros);
        }

        Dictionary<string, IReadOnlySet<string>> ofertados = oferta.Value!
            .Where(static v => v.Value is not null)
            .ToDictionary(static v => v.Key, static v => (IReadOnlySet<string>)v.Value!.Select(static o => o.Codigo).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            DefinicaoDoProcesso.Montar(
                processo.Formularios, processo.FatosColetados, processo.GruposColetados, processo.TermosExigidos,
                derivacoes.Value!, Agregados(processo, fatosDoCatalogo, catalogo)),
            new EntradaAvaliacaoFormulario(
                RespostaDeCampo.DentroDaOferta(simulacao.Respostas ?? new Dictionary<string, JsonElement>(), ofertados),
                new HashSet<string>(
                    (simulacao.EtapasConcluidas ?? [])
                        .Where(static e => e is not null)
                        .Select(static e => DefinicaoDoProcesso.CodigoDaEtapa(EstruturaFormulario.FinalidadeDoToken(e.Finalidade), e.Etapa)),
                    StringComparer.Ordinal),
                RespostaDeCampo.ComoFatosConhecidos(simulacao.Pressupostos),
                ocorrencias.ToDictionary(
                    static o => o.Key,
                    o => (IReadOnlyList<OcorrenciaRespondida>)[.. o.Value.Select(r => r with { Respostas = RespostaDeCampo.DentroDaOferta(r.Respostas, ofertados) })],
                    StringComparer.Ordinal)));

        // As ocorrências de cada grupo visível são as instâncias que a árvore repete; o grupo oculto
        // não tem instância, e a subárvore dele não se aplica.
        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = avaliacao.Grupos
            .Where(static g => g.Visivel == Ternario.Verdadeiro)
            .ToDictionary(
                static g => g.Codigo,
                static g => (IReadOnlyList<InstanciaEntidade>)[.. g.Ocorrencias.Select(static o => new InstanciaEntidade(o.Id, o.Fatos))],
                StringComparer.Ordinal);
        Result<ResultadoResolucaoArvore> arvore = ResolvedorArvoreSatisfacao.Resolver(
            ArvoreExigenciasCongelada.DaConfiguracaoViva(processo),
            avaliacao.Fatos,
            new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>(),
            instancias);
        if (arvore.IsFailure)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(arvore.Error!);
        }

        return Result<PreVisualizacaoDoProcessoDto>.Success(new PreVisualizacaoDoProcessoDto(
            Formularios(processo, avaliacao),
            Documentos(processo, arvore.Value!, avaliacao.Grupos.ToDictionary(static g => g.Codigo, StringComparer.Ordinal))));
    }

    /// <summary>
    /// As ocorrências simuladas de cada grupo; a identidade em branco ou repetida é recusada, porque
    /// é ela que correlaciona os documentos exigidos por membro (UNI-REQ-0069).
    /// </summary>
    private static (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> Ocorrencias, List<FieldError> Erros) LerOcorrencias(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaSimuladaInput>>? grupos)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias = new(StringComparer.Ordinal);
        List<FieldError> erros = [];
        foreach ((string grupo, IReadOnlyList<OcorrenciaSimuladaInput>? simuladas) in grupos ?? new Dictionary<string, IReadOnlyList<OcorrenciaSimuladaInput>>())
        {
            HashSet<string> vistas = new(StringComparer.Ordinal);
            List<OcorrenciaRespondida> respondidas = [];
            IReadOnlyList<OcorrenciaSimuladaInput> lista = simuladas ?? [];
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i]?.Id is not { } id || string.IsNullOrWhiteSpace(id) || !vistas.Add(id))
                {
                    erros.Add(new($"grupos.{grupo}[{i}].id", new DomainError(
                        OcorrenciaInvalida, "Cada ocorrência do grupo precisa de uma identidade própria, que não se repita no grupo.")));
                    continue;
                }

                respondidas.Add(new OcorrenciaRespondida(id, lista[i].Respostas ?? new Dictionary<string, JsonElement>()));
            }

            ocorrencias[grupo] = respondidas;
        }

        return (ocorrencias, erros);
    }

    /// <summary>
    /// As derivações do processo, cada uma conferida contra o domínio em que contribui: o dinâmico
    /// do processo, o do catálogo ou, quando o servidor não enumera a fonte, o das próprias regras.
    /// </summary>
    private static Result<IReadOnlyList<RegrasDerivacaoFato>> Derivacoes(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        Dictionary<string, DominioDeValores> dominios = VocabularioDeFatos.DominiosDinamicos(processo, catalogo.Values);
        List<RegrasDerivacaoFato> derivacoes = [];
        foreach (ConfiguracaoDerivacaoFato configuracao in processo.RegrasDerivacao.OrderBy(static c => c.CodigoFato, StringComparer.Ordinal))
        {
            IReadOnlyCollection<string> dominio =
                catalogo.TryGetValue(configuracao.CodigoFato, out FatoCandidatoView? fato) && VocabularioDeFatos.DominioDeContribuicao(fato, dominios) is { } doCatalogo
                    ? doCatalogo
                    : [.. configuracao.Regras.Select(static r => r.Contribui).OfType<string>().Distinct(StringComparer.Ordinal)];
            Result<RegrasDerivacaoFato> regras = configuracao.ParaRegrasDerivacao(dominio);
            if (regras.IsFailure)
            {
                return Result<IReadOnlyList<RegrasDerivacaoFato>>.Failure(regras.Error!);
            }

            derivacoes.Add(regras.Value!);
        }

        return Result<IReadOnlyList<RegrasDerivacaoFato>>.Success(derivacoes);
    }

    /// <summary>Os agregados do catálogo cujo fato de membro é campo de um grupo do processo.</summary>
    private static List<DefinicaoAgregado> Agregados(
        ProcessoSeletivo processo, IEnumerable<FatoCandidatoView> fatosDoCatalogo, Dictionary<string, FatoCandidatoView> catalogo)
    {
        Dictionary<string, string> grupoDoCampo = processo.GruposColetados
            .SelectMany(static g => g.Subitens.Select(s => (Campo: s.FatoCodigo, Grupo: g.Codigo)))
            .ToDictionary(static p => p.Campo, static p => p.Grupo, StringComparer.Ordinal);
        return [.. VocabularioDeFatos.MembroPorAgregado(fatosDoCatalogo)
            .Where(a => grupoDoCampo.ContainsKey(a.Value) && catalogo.ContainsKey(a.Value))
            .OrderBy(static a => a.Key, StringComparer.Ordinal)
            .Select(a => (Agregado: a.Key, Membro: a.Value, Operacao: AgregadoDeGrupo.OperacaoDoDominio(catalogo[a.Value].Dominio)))
            .Where(static a => a.Operacao != OperacaoAgregado.Nenhuma)
            .Select(a => new DefinicaoAgregado(a.Agregado, grupoDoCampo[a.Membro], a.Membro, a.Operacao))];
    }

    private static List<FormularioSimuladoDto> Formularios(ProcessoSeletivo processo, AvaliacaoFormulario avaliacao)
    {
        Dictionary<string, FatoColetado> itemPorFato = processo.FatosColetados.ToDictionary(static f => f.FatoCodigo, StringComparer.Ordinal);
        Dictionary<string, GrupoColetado> grupoPorCodigo = processo.GruposColetados.ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
        Dictionary<string, TermoExigidoFormulario> termoPorCodigo = processo.TermosExigidos
            .ToDictionary(static t => DefinicaoDoProcesso.CodigoDoTermo(t.Finalidade, t.Codigo), StringComparer.Ordinal);

        return [.. processo.Formularios.OrderBy(static f => f.Finalidade).Select(formulario => new FormularioSimuladoDto(
            EstruturaFormulario.ParaToken(formulario.Finalidade),
            [.. avaliacao.Itens
                .Where(i => itemPorFato[i.FatoCodigo].Finalidade == formulario.Finalidade)
                .Select(i => Item(i, itemPorFato[i.FatoCodigo]))],
            [.. avaliacao.Grupos
                .Where(g => grupoPorCodigo[g.Codigo].Finalidade == formulario.Finalidade)
                .Select(g => new GrupoSimuladoDto(
                    g.Codigo, grupoPorCodigo[g.Codigo].EtapaCodigo, g.Visivel.ToCodigo(), g.Obrigatorio.ToCodigo(),
                    g.ContagemValida, g.OcorrenciaDoCandidatoValida,
                    [.. g.Ocorrencias.Select(o => new OcorrenciaSimuladaDto(o.Id, [.. o.Itens.Select(i => Item(
                        i, grupoPorCodigo[g.Codigo].Subitens.Single(s => s.FatoCodigo == i.FatoCodigo), grupoPorCodigo[g.Codigo].EtapaCodigo))]))]))],
            [.. avaliacao.Termos
                .Where(t => termoPorCodigo[t.Codigo].Finalidade == formulario.Finalidade)
                .Select(t => new TermoSimuladoDto(termoPorCodigo[t.Codigo].Codigo, t.Visivel.ToCodigo(), t.Obrigatorio.ToCodigo()))]))];
    }

    /// <summary>O item avaliado; o campo de grupo segue a seção do grupo.</summary>
    private static ItemSimuladoDto Item(AvaliacaoItem item, FatoColetado campo, string? etapaDoGrupo = null) => new(
        item.FatoCodigo, etapaDoGrupo ?? campo.EtapaCodigo, item.Visivel.ToCodigo(), item.Obrigatorio.ToCodigo(),
        [.. item.RestricoesVioladas.Select(static r => RestricaoValorJson.ParaToken(r.Tipo))],
        item.Impedido.ToCodigo(), campo.Impedimento?.Mensagem);

    /// <summary>
    /// Cada folha da árvore com a situação dela diante do perfil: a de fora de repetição pelo status
    /// da exigência; a de dentro de um grupo repetível uma vez por ocorrência, uma vez indeterminada
    /// quando ainda não se sabe se o grupo aparece ou quais membros ele tem, e uma vez não exigida
    /// quando o grupo não aparece ou a lista dele vale sem ocorrência.
    /// </summary>
    private static List<DocumentoSimuladoDto> Documentos(
        ProcessoSeletivo processo, ResultadoResolucaoArvore resultado, IReadOnlyDictionary<string, AvaliacaoGrupo> grupos)
    {
        List<DocumentoSimuladoDto> documentos = [];
        foreach (NoExigencia raiz in processo.RaizesDeExigencia.OrderBy(static r => r.Ordem))
        {
            Coletar(raiz, alternativas: [], grupoRepetido: null, resultado, grupos, documentos);
        }

        return documentos;
    }

    /// <param name="alternativas">Os grupos de alternativas que contêm o nó, do mais externo ao mais interno.</param>
    /// <param name="grupoRepetido">O código do grupo que a subárvore repete; nulo fora de repetição.</param>
    private static void Coletar(
        NoExigencia no,
        IReadOnlyList<AlternativasSimuladasDto> alternativas,
        string? grupoRepetido,
        ResultadoResolucaoArvore resultado,
        IReadOnlyDictionary<string, AvaliacaoGrupo> grupos,
        List<DocumentoSimuladoDto> documentos)
    {
        string? repetido = no.RepetePorEntidade ?? grupoRepetido;
        if (no.Tipo != TipoNo.Folha)
        {
            IReadOnlyList<AlternativasSimuladasDto> dosFilhos = no.Tipo == TipoNo.GrupoOu
                ? [.. alternativas, new AlternativasSimuladasDto(no.Id, no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao)]
                : alternativas;
            foreach (NoExigencia filho in no.Filhos.OrderBy(static f => f.Ordem))
            {
                Coletar(filho, dosFilhos, repetido, resultado, grupos, documentos);
            }

            return;
        }

        DocumentoExigido documento = no.DocumentoExigido!;
        DocumentoSimuladoDto Documento(StatusResolucaoExigencia status, string? entidadeId) => new(
            documento.Id, documento.TipoDocumentoCodigo, documento.TipoDocumentoNome, documento.Obrigatorio,
            documento.ExigidoNaFaseId, documento.ExigidoNaEtapaId, Situacao(status), entidadeId, alternativas);

        if (repetido is null)
        {
            documentos.Add(Documento(resultado.StatusPorExigencia.GetValueOrDefault(documento.Id), entidadeId: null));
            return;
        }

        AvaliacaoGrupo? grupo = grupos.GetValueOrDefault(repetido);
        if (grupo?.Visivel == Ternario.Indeterminado)
        {
            documentos.Add(Documento(StatusResolucaoExigencia.AplicabilidadeIndeterminada, entidadeId: null));
            return;
        }

        List<DocumentoSimuladoDto> porOcorrencia = [.. resultado.StatusPorEntidade
            .Where(s => s.DocumentoExigidoId == documento.Id)
            .Select(s => Documento(s.Status, s.EntidadeId))];
        if (porOcorrencia.Count > 0)
        {
            documentos.AddRange(porOcorrencia);
            return;
        }

        // Sem ocorrência, o grupo pendente — visível e sem resposta, ou com uma lista que não vale —
        // ainda não diz quais membros existem: a exigência por membro fica indeterminada, nunca
        // descartada (UNI-REQ-0064). Só o grupo oculto ou a lista vazia que vale a descartam.
        documentos.Add(Documento(
            grupo?.Estado == EstadoFato.Indeterminado ? StatusResolucaoExigencia.AplicabilidadeIndeterminada : StatusResolucaoExigencia.NaoAplicavel,
            entidadeId: null));
    }

    private static string Situacao(StatusResolucaoExigencia status) => status switch
    {
        StatusResolucaoExigencia.Pendente or StatusResolucaoExigencia.Satisfeita => "EXIGIDO",
        StatusResolucaoExigencia.NaoAplicavel => "NAO_EXIGIDO",
        _ => "INDETERMINADO",
    };
}
