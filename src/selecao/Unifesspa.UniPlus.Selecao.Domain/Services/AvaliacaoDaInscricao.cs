namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// Avalia o que depende dos fatos do candidato (ADR-0143): o formulário inteiro, a árvore de
/// exigências sobre essa mesma avaliação e as modalidades em que ele concorre (UNI-REQ-0076,
/// UNI-REQ-0064, UNI-REQ-0069). É a única composição do avaliador do formulário com o resolvedor da
/// árvore: a consulta do candidato, o rascunho e a pré-visualização do processo leem a mesma conta.
/// </summary>
/// <remarks>Função pura: não lê nem grava nada, e a mesma entrada dá sempre o mesmo resultado.</remarks>
public static class AvaliacaoDaInscricao
{
    /// <summary>O fato que diz em quais modalidades o candidato concorre.</summary>
    public const string FatoDaModalidade = "MODALIDADE";

    private static readonly string EtapaDaInscricao = DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, null);

    public static Result<ResultadoDaAvaliacaoDaInscricao> Avaliar(ConfiguracaoAvaliavel configuracao, EntradaDaAvaliacaoDaInscricao entrada)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(entrada);

        AvaliacaoFormulario formulario = AvaliadorFormulario.Avaliar(
            configuracao.Definicao,
            new EntradaAvaliacaoFormulario(
                RespostaDeCampo.DentroDaOferta(entrada.Respostas, configuracao.Ofertas),
                entrada.EtapasConcluidas,
                entrada.FatosConhecidos,
                entrada.Envio,
                entrada.Grupos.ToDictionary(
                    static g => g.Key,
                    g => (IReadOnlyList<OcorrenciaRespondida>)[.. g.Value.Select(o => o with { Respostas = RespostaDeCampo.DentroDaOferta(o.Respostas, configuracao.Ofertas) })],
                    StringComparer.Ordinal)));

        // As ocorrências de cada grupo visível são as instâncias que a árvore repete; o grupo oculto
        // não tem instância, e a subárvore dele não se aplica.
        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = formulario.Grupos
            .Where(static g => g.Visivel == Ternario.Verdadeiro)
            .ToDictionary(
                static g => g.Codigo,
                static g => (IReadOnlyList<InstanciaEntidade>)[.. g.Ocorrencias.Select(static o => new InstanciaEntidade(o.Id, o.Fatos))],
                StringComparer.Ordinal);
        Result<ResultadoResolucaoArvore> arvore = ResolvedorArvoreSatisfacao.Resolver(
            configuracao.Arvore,
            formulario.Fatos,
            new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>(),
            instancias);
        if (arvore.IsFailure)
        {
            return Result<ResultadoDaAvaliacaoDaInscricao>.Failure(arvore.Error!);
        }

        return Result<ResultadoDaAvaliacaoDaInscricao>.Success(new ResultadoDaAvaliacaoDaInscricao(
            formulario,
            Modalidades(configuracao, formulario),
            Documentos(configuracao.Arvore, arvore.Value!, formulario.Grupos.ToDictionary(static g => g.Codigo, StringComparer.Ordinal))));
    }

    /// <summary>
    /// O que o formulário da inscrição pergunta e de que a avaliação depende, para quem consulta saber
    /// quando a resposta muda a avaliação: os campos alcançados pelas derivações — o derivado do
    /// sistema, pelos campos de que ele é calculado — e os grupos que os agregados alcançados somam.
    /// </summary>
    public static DependenciasDaAvaliacao Dependencias(DefinicaoFormulario definicao)
    {
        ArgumentNullException.ThrowIfNull(definicao);

        if (!DerivaAModalidade(definicao))
        {
            return new DependenciasDaAvaliacao([], []);
        }

        FechamentoDasDependencias fechamento = FechamentoDaModalidade(definicao);
        HashSet<string> camposDaInscricao = new(
            EtapasDaInscricao(definicao).SelectMany(static e => e.Itens).Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        HashSet<string> gruposDaInscricao = new(
            EtapasDaInscricao(definicao).SelectMany(static e => e.Grupos).Select(static g => g.Codigo), StringComparer.Ordinal);
        DefinicaoAgregado[] agregados = [.. definicao.Agregados
            .Where(a => fechamento.Agregados.Contains(a.Codigo) && gruposDaInscricao.Contains(a.GrupoCodigo))];

        return new DependenciasDaAvaliacao(
            [.. CamposAlcancados(fechamento)
                .Where(camposDaInscricao.Contains)
                .Concat(agregados.Select(static a => $"{a.GrupoCodigo}.{a.FatoDeMembro}"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            [.. agregados.Select(static a => a.GrupoCodigo).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Os derivados do sistema de que a avaliação depende — os únicos que vale apurar antes dela,
    /// porque apurar o resto não muda a conta e pode depender de consulta externa.
    /// </summary>
    public static IReadOnlySet<string> DerivadosDoSistemaCitados(DefinicaoFormulario definicao)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        return DerivaAModalidade(definicao)
            ? FechamentoDaModalidade(definicao).Pressupostos.Where(DerivadosDoSistema.Dependencias.ContainsKey).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    private static bool DerivaAModalidade(DefinicaoFormulario definicao) =>
        definicao.Derivacoes.Any(static d => d.CodigoFato == FatoDaModalidade);

    /// <summary>
    /// O que a modalidade alcança: as dependências das derivações e, de cada campo e agregado
    /// alcançado, o que decide se ele aparece e é exigido — a resposta que esconde o campo também muda
    /// a modalidade, porque a resposta escondida deixa de valer.
    /// </summary>
    private static FechamentoDasDependencias FechamentoDaModalidade(DefinicaoFormulario definicao)
    {
        Dictionary<string, IReadOnlyCollection<string>> condicoes = new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyCollection<string>> condicoesDoGrupo = new(StringComparer.Ordinal);
        foreach (DefinicaoEtapa etapa in definicao.Etapas)
        {
            IReadOnlyCollection<string> daSecao = etapa.Exibicao?.FatosCitados ?? [];
            foreach (DefinicaoItem item in etapa.Itens)
            {
                condicoes[item.FatoCodigo] = [.. daSecao, .. item.FatosCitados];
            }

            foreach (DefinicaoGrupo grupo in etapa.Grupos)
            {
                condicoesDoGrupo[grupo.Codigo] = [.. daSecao, .. grupo.FatosDoCandidatoCitados];
            }
        }

        foreach (DefinicaoAgregado agregado in definicao.Agregados)
        {
            condicoes[agregado.Codigo] = condicoesDoGrupo.GetValueOrDefault(agregado.GrupoCodigo) ?? [];
        }

        return FechamentoDasDependencias.De(
            [FatoDaModalidade],
            definicao.Etapas.SelectMany(static e => e.Itens).Select(static i => i.FatoCodigo).ToHashSet(StringComparer.Ordinal),
            definicao.Derivacoes.ToDictionary(static d => d.CodigoFato, StringComparer.Ordinal),
            definicao.Agregados.ToDictionary(static a => a.Codigo, StringComparer.Ordinal),
            condicoes);
    }

    /// <summary>Os campos perguntados que o fechamento alcança, direto ou pelo derivado do sistema que se calcula deles.</summary>
    private static IEnumerable<string> CamposAlcancados(FechamentoDasDependencias fechamento) =>
        fechamento.Perguntados.Concat(fechamento.Pressupostos
            .Where(DerivadosDoSistema.Dependencias.ContainsKey)
            .SelectMany(static p => DerivadosDoSistema.Dependencias[p]));

    private static IEnumerable<DefinicaoEtapa> EtapasDaInscricao(DefinicaoFormulario definicao) =>
        definicao.Etapas.Where(static e => e.Codigo == EtapaDaInscricao || e.Codigo.StartsWith(EtapaDaInscricao + ":", StringComparison.Ordinal));

    private static ModalidadesDeConcorrencia Modalidades(ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario)
    {
        if (!DerivaAModalidade(configuracao.Definicao))
        {
            return ModalidadesDeConcorrencia.SemDerivacao;
        }

        FatoResolvido modalidade = formulario.Fatos.GetValueOrDefault(FatoDaModalidade) ?? FatoResolvido.Indeterminado();
        if (modalidade is not { Estado: EstadoFato.Resolvido, Valor: { } valor })
        {
            return new ModalidadesDeConcorrencia(
                true, SituacaoDasModalidades.FaltaResponder, [], Pendencias(configuracao, formulario));
        }

        // A publicação confere cada contribuição da derivação contra as modalidades ofertadas: todo
        // código derivado tem nome na distribuição de vagas.
        Dictionary<string, ModalidadeDeConcorrencia> ofertadas = Ofertadas(configuracao.ModalidadesOfertadas);
        ModalidadeDeConcorrencia[] itens = [.. (RespostaDeCampo.Codigos(valor) ?? [])
            .Where(ofertadas.ContainsKey)
            .Select(c => ofertadas[c])
            .OrderBy(static m => m.Ordem)];

        return new ModalidadesDeConcorrencia(
            true, itens.Length > 0 ? SituacaoDasModalidades.Concorre : SituacaoDasModalidades.NaoConcorre, itens, []);
    }

    /// <summary>
    /// As modalidades ofertadas, pelo código — a mesma modalidade se repete em cada oferta de curso —,
    /// com o nome congelado e a posição na ordem ordinal dos códigos, a mesma em que a publicação
    /// congela o domínio da modalidade.
    /// </summary>
    private static Dictionary<string, ModalidadeDeConcorrencia> Ofertadas(IEnumerable<ModalidadeSelecionada> modalidades) =>
        modalidades
            .GroupBy(static m => m.Codigo, StringComparer.Ordinal)
            .OrderBy(static g => g.Key, StringComparer.Ordinal)
            .Select(static (g, ordem) => new ModalidadeDeConcorrencia(
                g.Key, g.Select(static m => m.Descricao).FirstOrDefault(static d => !string.IsNullOrWhiteSpace(d)) ?? g.Key, ordem))
            .ToDictionary(static m => m.Codigo, StringComparer.Ordinal);

    /// <summary>
    /// O que falta responder, na ordem do formulário: os campos e os grupos visíveis da inscrição de que
    /// a derivação depende e que continuam indeterminados. O campo cuja exibição ainda não se decide não
    /// entra: o que falta é a resposta que decide se ele aparece.
    /// </summary>
    private static List<PendenciaDaAvaliacao> Pendencias(ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario)
    {
        DependenciasDaAvaliacao dependencias = Dependencias(configuracao.Definicao);
        HashSet<string> campos = new(dependencias.Fatos, StringComparer.Ordinal);
        HashSet<string> grupos = new(dependencias.Grupos, StringComparer.Ordinal);
        Dictionary<string, AvaliacaoItem> itemPorFato = formulario.Itens.ToDictionary(static i => i.FatoCodigo, StringComparer.Ordinal);
        Dictionary<string, AvaliacaoGrupo> grupoPorCodigo = formulario.Grupos.ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
        Dictionary<string, FatoColetado> campoPorFato = configuracao.Campos
            .Where(static c => c.Finalidade == FinalidadeFormulario.Inscricao)
            .ToDictionary(static c => c.FatoCodigo, StringComparer.Ordinal);
        Dictionary<string, GrupoColetado> grupoColetado = configuracao.Grupos
            .Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
            .ToDictionary(static g => g.Codigo, StringComparer.Ordinal);

        List<PendenciaDaAvaliacao> pendencias = [];
        foreach (DefinicaoEtapa etapa in EtapasDaInscricao(configuracao.Definicao))
        {
            foreach (DefinicaoItem item in etapa.Itens.Where(i => campos.Contains(i.FatoCodigo)))
            {
                if (itemPorFato.GetValueOrDefault(item.FatoCodigo) is { Visivel: Ternario.Verdadeiro }
                    && formulario.Fatos.GetValueOrDefault(item.FatoCodigo)?.Estado == EstadoFato.Indeterminado
                    && campoPorFato.TryGetValue(item.FatoCodigo, out FatoColetado? campo))
                {
                    pendencias.Add(new PendenciaDaAvaliacao(campo.FatoCodigo, campo.Rotulo, campo.EtapaCodigo));
                }
            }

            foreach (DefinicaoGrupo grupo in etapa.Grupos.Where(g => grupos.Contains(g.Codigo)))
            {
                if (grupoPorCodigo.GetValueOrDefault(grupo.Codigo) is { Visivel: Ternario.Verdadeiro, Estado: EstadoFato.Indeterminado }
                    && grupoColetado.TryGetValue(grupo.Codigo, out GrupoColetado? coletado))
                {
                    pendencias.Add(new PendenciaDaAvaliacao(coletado.Codigo, coletado.Rotulo, coletado.EtapaCodigo));
                }
            }
        }

        return pendencias;
    }

    /// <summary>
    /// Cada folha da árvore com a situação dela diante do candidato: a de fora de repetição pelo status
    /// da exigência; a de dentro de um grupo repetível uma vez por ocorrência, uma vez indeterminada
    /// quando ainda não se sabe se o grupo aparece ou quais membros ele tem, e uma vez não exigida
    /// quando o grupo não aparece ou a lista dele vale sem ocorrência.
    /// </summary>
    private static List<DocumentoAvaliado> Documentos(
        ArvoreExigenciasCongelada arvore, ResultadoResolucaoArvore resultado, IReadOnlyDictionary<string, AvaliacaoGrupo> grupos)
    {
        List<DocumentoAvaliado> documentos = [];
        foreach (NoExigencia raiz in arvore.Raizes.OrderBy(static r => r.Ordem))
        {
            Coletar(raiz, alternativas: [], grupoRepetido: null, resultado, grupos, documentos);
        }

        return documentos;
    }

    /// <param name="alternativas">Os grupos de alternativas que contêm o nó, do mais externo ao mais interno.</param>
    /// <param name="grupoRepetido">O código do grupo que a subárvore repete; nulo fora de repetição.</param>
    private static void Coletar(
        NoExigencia no,
        IReadOnlyList<AlternativaDeDocumento> alternativas,
        string? grupoRepetido,
        ResultadoResolucaoArvore resultado,
        IReadOnlyDictionary<string, AvaliacaoGrupo> grupos,
        List<DocumentoAvaliado> documentos)
    {
        string? repetido = no.RepetePorEntidade ?? grupoRepetido;
        if (no.Tipo != TipoNo.Folha)
        {
            IReadOnlyList<AlternativaDeDocumento> dosFilhos = no.Tipo == TipoNo.GrupoOu
                ? [.. alternativas, new AlternativaDeDocumento(no.Id, no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao)]
                : alternativas;
            foreach (NoExigencia filho in no.Filhos.OrderBy(static f => f.Ordem))
            {
                Coletar(filho, dosFilhos, repetido, resultado, grupos, documentos);
            }

            return;
        }

        DocumentoExigido documento = no.DocumentoExigido!;
        if (repetido is null)
        {
            documentos.Add(new(documento, resultado.StatusPorExigencia.GetValueOrDefault(documento.Id), null, alternativas));
            return;
        }

        AvaliacaoGrupo? grupo = grupos.GetValueOrDefault(repetido);
        if (grupo?.Visivel == Ternario.Indeterminado)
        {
            documentos.Add(new(documento, StatusResolucaoExigencia.AplicabilidadeIndeterminada, null, alternativas));
            return;
        }

        List<DocumentoAvaliado> porOcorrencia = [.. resultado.StatusPorEntidade
            .Where(s => s.DocumentoExigidoId == documento.Id)
            .Select(s => new DocumentoAvaliado(documento, s.Status, s.EntidadeId, alternativas))];
        if (porOcorrencia.Count > 0)
        {
            documentos.AddRange(porOcorrencia);
            return;
        }

        // Sem ocorrência, o grupo pendente — visível e sem resposta, ou com uma lista que não vale —
        // ainda não diz quais membros existem: a exigência por membro fica indeterminada, nunca
        // descartada (UNI-REQ-0064). Só o grupo oculto ou a lista vazia que vale a descartam.
        documentos.Add(new(
            documento,
            grupo?.Estado == EstadoFato.Indeterminado ? StatusResolucaoExigencia.AplicabilidadeIndeterminada : StatusResolucaoExigencia.NaoAplicavel,
            null,
            alternativas));
    }
}
