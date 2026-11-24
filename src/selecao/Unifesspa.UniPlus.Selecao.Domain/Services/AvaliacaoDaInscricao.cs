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
/// Avalia o formulário, as regras de comprovação e as modalidades de concorrência do candidato (ADR-0143).
/// A mesma avaliação atende à consulta, ao rascunho e à pré-visualização do processo.
/// </summary>
/// <remarks>Função pura: não lê nem grava nada, e a mesma entrada dá sempre o mesmo resultado.</remarks>
public static class AvaliacaoDaInscricao
{
    /// <summary>O fato que diz em quais modalidades o candidato concorre.</summary>
    public const string FatoDaModalidade = ModalidadeDoCandidato.Fato;

    /// <summary>O fato que diz em quais modalidades o candidato concorre na 2ª opção de curso.</summary>
    public const string FatoDaModalidadeDaSegundaOpcao = ModalidadeDoCandidato.FatoSegundaOpcao;

    /// <summary>Como a modalidade derivada é gravada: os códigos das modalidades, um por linha, na ordem das ofertadas.</summary>
    public static DefinicaoDaResposta DefinicaoDaModalidade { get; } = new(TipoDoValorDeResposta.Opcao, Multivalorado: true, Identificador: false);

    /// <summary>
    /// Só as modalidades em que o candidato concorre, sem avaliar as regras de comprovação nem os documentos: para
    /// quem avalia a mesma entrada diante da distribuição de vagas de cada oferta e precisa só delas.
    /// </summary>
    public static ModalidadesDeConcorrencia AvaliarModalidades(ConfiguracaoAvaliavel configuracao, EntradaDaAvaliacaoDaInscricao entrada)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(entrada);

        return Modalidades(configuracao, AvaliarFormulario(configuracao, entrada));
    }

    public static Result<ResultadoDaAvaliacaoDaInscricao> Avaliar(ConfiguracaoAvaliavel configuracao, EntradaDaAvaliacaoDaInscricao entrada)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(entrada);

        AvaliacaoFormulario formulario = AvaliarFormulario(configuracao, entrada);

        IReadOnlySet<Guid> deFasePosterior = entrada.NaFaseDaInscricao
            ? ExigenciasDeFasePosterior.Na(configuracao, entrada.FatosConhecidos)
            : new HashSet<Guid>();
        Result<ResultadoDaComprovacao> resultadoDaComprovacao = AvaliadorDaComprovacao.Resolver(
            configuracao.RegrasDeComprovacao, formulario.Fatos, entrada.Apresentacoes, OcorrenciasVisiveis(formulario), deFasePosterior);
        return ComporSeResolvida(configuracao, formulario, resultadoDaComprovacao, deFasePosterior);
    }

    private static Result<ResultadoDaAvaliacaoDaInscricao> ComporSeResolvida(
        ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario,
        Result<ResultadoDaComprovacao> resultadoDaComprovacao, IReadOnlySet<Guid> deFasePosterior) =>
        resultadoDaComprovacao.IsFailure
            ? Result<ResultadoDaAvaliacaoDaInscricao>.Failure(resultadoDaComprovacao.Error!)
            : Result<ResultadoDaAvaliacaoDaInscricao>.Success(ComporResultado(configuracao, formulario, resultadoDaComprovacao.Value!, deFasePosterior));

    private static Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> OcorrenciasVisiveis(AvaliacaoFormulario formulario) =>
        formulario.Grupos.Where(static g => g.Visivel == Ternario.Verdadeiro).ToDictionary(
            static g => g.Codigo,
            static g => (IReadOnlyList<OcorrenciaDoGrupo>)[.. g.Ocorrencias.Select(static o => new OcorrenciaDoGrupo(o.Id, o.Fatos))],
            StringComparer.Ordinal);

    private static ResultadoDaAvaliacaoDaInscricao ComporResultado(
        ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario, ResultadoDaComprovacao resultadoDaComprovacao, IReadOnlySet<Guid> deFasePosterior)
    {
        List<DocumentoAvaliado> documentos = new ColetorDeDocumentosAvaliados(
            resultadoDaComprovacao, formulario.Grupos.ToDictionary(static g => g.Codigo, StringComparer.Ordinal), deFasePosterior).Coletar(configuracao.RegrasDeComprovacao);
        return new ResultadoDaAvaliacaoDaInscricao(
            formulario,
            Modalidades(configuracao, formulario),
            new DocumentosDaInscricao(
                documentos,
                DocumentosAvaliadosDaInscricao.PendenciasDosDocumentos(configuracao, formulario, documentos),
                DocumentosAusentesDaInscricao.Apurar(configuracao.RegrasDeComprovacao, resultadoDaComprovacao)));
    }

    private static AvaliacaoFormulario AvaliarFormulario(ConfiguracaoAvaliavel configuracao, EntradaDaAvaliacaoDaInscricao entrada) =>
        AvaliadorFormulario.Avaliar(
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

    /// <summary>
    /// O que o formulário da inscrição pergunta e de que a avaliação depende, para quem consulta saber
    /// quando a resposta muda a avaliação: os campos alcançados pelas derivações e pelos gatilhos das
    /// exigências — o derivado do sistema, pelos campos de que ele é calculado —, os grupos que os
    /// agregados alcançados somam, os grupos pelos quais as regras de comprovação se repetem, porque incluir
    /// ou tirar um membro muda o que se exige, e o que decide se esses grupos aparecem.
    /// </summary>
    public static DependenciasDaAvaliacao Dependencias(ConfiguracaoAvaliavel configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        DependenciasDaAvaliacao daModalidade = DependenciasDaModalidade(configuracao.Definicao);
        DependenciasDaAvaliacao dosDocumentos = DependenciasDaInscricao.DependenciasDe(configuracao.Definicao, new ColetorDeCitacoes(configuracao.Definicao, null).Coletar(configuracao.RegrasDeComprovacao.RegrasSemGrupo));
        return new DependenciasDaAvaliacao(
            [.. daModalidade.Fatos.Union(dosDocumentos.Fatos, StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. daModalidade.Grupos.Union(dosDocumentos.Grupos, StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// De que campos e grupos do formulário da inscrição depende o Fator de Atenuação (ADR-0142), só pelo
    /// código: o fato de elegibilidade que o bônus regional cita — ou, sendo derivado, os campos de que a
    /// derivação depende — e os campos cujos valores são os municípios do bônus, com o que decide se eles
    /// aparecem e são exigidos. O fato é do administrador: nada aqui nomeia código. Sem bônus regional,
    /// o processo não tem Fator e a lista é vazia.
    /// </summary>
    public static DependenciasDaAvaliacao DependenciasDoFator(ConfiguracaoAvaliavel configuracao, ConfiguracaoBonusRegional? bonus)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (bonus is null)
        {
            return new DependenciasDaAvaliacao([], []);
        }

        string[] doCandidato = FatosDoFator(configuracao, bonus);
        (string Grupo, string Fato)[] deMembro = FatosDeMembroDoFator(configuracao);
        return DependenciasDaInscricao.DependenciasDe(
            configuracao.Definicao,
            new DependenciasDaInscricao.CitacoesDosGatilhos(doCandidato, deMembro, [.. deMembro.Select(static m => m.Grupo).Distinct(StringComparer.Ordinal)]));
    }

    private static string[] FatosDoFator(ConfiguracaoAvaliavel configuracao, ConfiguracaoBonusRegional bonus) =>
    [
        .. bonus.FatoElegibilidadeCodigo is { } elegibilidade ? [elegibilidade] : Array.Empty<string>(),
        .. configuracao.Campos.Where(static c => c.OrigemValores == OrigemValoresColeta.MunicipiosDoBonus).Select(static c => c.FatoCodigo),
    ];

    private static (string Grupo, string Fato)[] FatosDeMembroDoFator(ConfiguracaoAvaliavel configuracao) =>
    [
        .. configuracao.Grupos.SelectMany(static g => g.Subitens
            .Where(static s => s.OrigemValores == OrigemValoresColeta.MunicipiosDoBonus)
            .Select(s => (Grupo: g.Codigo, Fato: s.FatoCodigo))),
    ];

    /// <summary>
    /// Os derivados do sistema de que a avaliação depende — os únicos que vale apurar antes dela,
    /// porque apurar o resto não muda a conta e pode depender de consulta externa.
    /// </summary>
    public static IReadOnlySet<string> DerivadosDoSistemaCitados(ConfiguracaoAvaliavel configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        IEnumerable<string> alvos = DependenciasDaInscricao.AlvosDoFechamento(configuracao.Definicao, new ColetorDeCitacoes(configuracao.Definicao, null).Coletar(configuracao.RegrasDeComprovacao.RegrasSemGrupo));
        if (DerivaAModalidade(configuracao.Definicao))
        {
            alvos = alvos.Append(FatoDaModalidade);
        }

        return DependenciasDaInscricao.Fechamento(configuracao.Definicao, alvos).Pressupostos.Where(DerivadosDoSistema.Dependencias.ContainsKey).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Os derivados do sistema de que a modalidade deriva — só a modalidade, e não o resto da avaliação —, como a UF e o município
    /// de residência. Vazio quando a configuração não deriva a modalidade.
    /// </summary>
    public static IReadOnlySet<string> DerivadosDoSistemaDaModalidade(ConfiguracaoAvaliavel configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return DerivaAModalidade(configuracao.Definicao)
            ? DependenciasDaInscricao.Fechamento(configuracao.Definicao, [FatoDaModalidade]).Pressupostos.Where(DerivadosDoSistema.Dependencias.ContainsKey).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    private static bool DerivaAModalidade(DefinicaoFormulario definicao) =>
        definicao.Derivacoes.Any(static d => d.CodigoFato == FatoDaModalidade);

    /// <summary>Os campos e os grupos da inscrição de que a modalidade depende.</summary>
    private static DependenciasDaAvaliacao DependenciasDaModalidade(DefinicaoFormulario definicao) =>
        DerivaAModalidade(definicao)
            ? DependenciasDaInscricao.DependenciasDe(definicao, new DependenciasDaInscricao.CitacoesDosGatilhos([FatoDaModalidade], [], []))
            : new DependenciasDaAvaliacao([], []);

    /// <summary>
    /// A avaliação só com o que o formulário de inscrição pergunta: as etapas, os itens, os grupos e os
    /// termos da finalidade Inscrição. Os fatos ficam todos, porque a derivação pode citar um fato que
    /// outra finalidade coleta. É sobre ela que se decide o veredito da inscrição: o item obrigatório de
    /// outra finalidade, como a isenção, não é pendência da inscrição.
    /// </summary>
    public static AvaliacaoFormulario DaInscricao(AvaliacaoFormulario formulario)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        return formulario with
        {
            Etapas = [.. formulario.Etapas.Where(static e => DaInscricao(e.Codigo))],
            Itens = [.. formulario.Itens.Where(static i => DaInscricao(i.EtapaCodigo))],
            Grupos = [.. formulario.Grupos.Where(static g => DaInscricao(g.EtapaCodigo))],
            Termos = [.. formulario.Termos.Where(static t => DaInscricao(t.Codigo))],
        };
    }

    public static IReadOnlySet<string> CodigosDasEtapasDaInscricao(DefinicaoFormulario definicao)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        return EtapasDaInscricao(definicao).Select(static e => e.Codigo).ToHashSet(StringComparer.Ordinal);
    }

    private static bool DaInscricao(string codigo) => DefinicaoDoProcesso.EhDaFinalidade(codigo, FinalidadeFormulario.Inscricao);

    internal static IEnumerable<DefinicaoEtapa> EtapasDaInscricao(DefinicaoFormulario definicao) =>
        definicao.Etapas.Where(static e => DaInscricao(e.Codigo));

    private static ModalidadesDeConcorrencia Modalidades(ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario)
    {
        if (!DerivaAModalidade(configuracao.Definicao))
        {
            return ModalidadesDeConcorrencia.SemDerivacao;
        }

        DependenciasDaAvaliacao dependencias = DependenciasDaModalidade(configuracao.Definicao);
        FatoResolvido modalidade = formulario.Fatos.GetValueOrDefault(FatoDaModalidade) ?? FatoResolvido.Indeterminado();
        if (modalidade is not { Estado: EstadoFato.Resolvido, Valor: { } valor })
        {
            return new ModalidadesDeConcorrencia(
                SituacaoDasModalidades.FaltaResponder, [], new PendenciasDaInscricao(configuracao, formulario, dependencias).Apurar(), dependencias);
        }

        return ModalidadesResolvidas(configuracao, dependencias, valor);
    }

    private static ModalidadesDeConcorrencia ModalidadesResolvidas(
        ConfiguracaoAvaliavel configuracao, DependenciasDaAvaliacao dependencias, JsonElement valor)
    {
        Dictionary<string, ModalidadeDeConcorrencia> ofertadas = Ofertadas(configuracao.ModalidadesOfertadas);
        ModalidadeDeConcorrencia[] itens = [.. (RespostaDeCampo.Codigos(valor) ?? [])
            .Where(ofertadas.ContainsKey)
            .Select(c => ofertadas[c])
            .OrderBy(static m => m.Ordem)];

        return new ModalidadesDeConcorrencia(
            itens.Length > 0 ? SituacaoDasModalidades.Concorre : SituacaoDasModalidades.NaoConcorre, itens, [], dependencias);
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

}
