namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A configuração do processo que a avaliação da inscrição lê — a congelada na versão que a inscrição
/// fixou (UNI-REQ-0084) ou, na pré-visualização, a viva do rascunho: a definição avaliável inteira, a
/// oferta de valores de cada campo, as regras de comprovação, os campos e os grupos que dão o rótulo e a
/// seção do que falta responder, as modalidades que a distribuição de vagas oferta, com o nome
/// congelado de cada uma, e as fases e os formulários, que dizem em que fase cada finalidade pergunta.
/// </summary>
public sealed record ConfiguracaoAvaliavel(
    DefinicaoFormulario Definicao,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas,
    RegrasDeComprovacaoDaVersao RegrasDeComprovacao,
    IReadOnlyCollection<FatoColetado> Campos,
    IReadOnlyCollection<GrupoColetado> Grupos,
    IReadOnlyCollection<ModalidadeSelecionada> ModalidadesOfertadas,
    IReadOnlyCollection<FaseCronograma> Fases,
    IReadOnlyCollection<FormularioProcesso> Formularios);

/// <summary>
/// O que o candidato respondeu e o que se sabe dele de fora do formulário: as respostas e os grupos;
/// os fatos já conhecidos — os pressupostos e os derivados do sistema que se conseguiu apurar —; e as
/// páginas dadas como concluídas, que decidem se o opcional em branco é não informado; e o instante
/// em que as respostas são gravadas, de que dependem os limites de data relativos a ele — nulo na
/// pré-visualização, que não grava. <see cref="NaFaseDaInscricao"/> diz que quem avalia é o candidato,
/// na fase da inscrição: o que só se conhece numa fase posterior não decide documento nenhum. A
/// pré-visualização simula o perfil inteiro, e por isso não o diz. <see cref="Apresentacoes"/> são os
/// documentos que o candidato enviou e foram confirmados, por exigência (<c>DocumentoExigido.Id</c>).
/// </summary>
public sealed record EntradaDaAvaliacaoDaInscricao(
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRespondida>> Grupos,
    IReadOnlyDictionary<string, FatoResolvido> FatosConhecidos,
    IReadOnlySet<string> PaginasConcluidas,
    EnvioDasRespostas? Envio,
    IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Apresentacoes,
    bool NaFaseDaInscricao = false)
{
    /// <summary>Sem documento enviado: o rascunho que ainda não tem arquivo, a pré-visualização e o salvamento.</summary>
    public static IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> SemApresentacoes { get; } =
        new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>();

    /// <summary>
    /// Monta a entrada entregando às regras o município sempre pelo código. A inscrição enviada guarda o município com o
    /// nome conferido, e as regras comparam o código como texto: por aqui o objeto gravado nunca chega a elas, nas
    /// respostas, nos subitens de grupo nem nos fatos já conhecidos.
    /// </summary>
    /// <param name="definicoes">A definição de cada resposta e de cada fato conhecido, pelo código do fato; diz quais são município.</param>
    public static EntradaDaAvaliacaoDaInscricao Montar(
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, EntradaDaAvaliacaoDaInscricao entrada)
    {
        ArgumentNullException.ThrowIfNull(definicoes);
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(entrada.Respostas, nameof(entrada));
        ArgumentNullException.ThrowIfNull(entrada.Grupos, nameof(entrada));
        ArgumentNullException.ThrowIfNull(entrada.FatosConhecidos, nameof(entrada));
        return entrada with
        {
            Respostas = ComoCodigos(definicoes, entrada.Respostas),
            Grupos = entrada.Grupos.ToDictionary(static g => g.Key,
                g => (IReadOnlyList<OcorrenciaRespondida>)[.. g.Value.Select(o => new OcorrenciaRespondida(o.Id, ComoCodigos(definicoes, o.Respostas)))], StringComparer.Ordinal),
            FatosConhecidos = entrada.FatosConhecidos.ToDictionary(static f => f.Key,
                f => ComoCodigo(definicoes, f.Key, f.Value), StringComparer.Ordinal),
        };
    }

    private static Dictionary<string, JsonElement> ComoCodigos(
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, IReadOnlyDictionary<string, JsonElement> valores) =>
        valores.ToDictionary(static v => v.Key,
            v => EhMunicipio(definicoes, v.Key) ? ValorDeMunicipio.ComoCodigo(v.Value) : v.Value, StringComparer.Ordinal);

    private static FatoResolvido ComoCodigo(IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, string codigo, FatoResolvido fato) =>
        EhMunicipio(definicoes, codigo) && fato is { Estado: EstadoFato.Resolvido, Valor: { } valor }
            ? FatoResolvido.Resolvido(ValorDeMunicipio.ComoCodigo(valor)) : fato;

    private static bool EhMunicipio(IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, string codigo) =>
        definicoes.TryGetValue(codigo, out DefinicaoDaResposta? definicao) && definicao.EhMunicipio;

}

/// <summary>
/// O resultado de uma avaliação só: a do formulário, a de cada exigência diante do candidato
/// e as modalidades em que ele concorre.
/// </summary>
/// <remarks>
/// Com duas opções de curso, o formulário e os documentos são avaliados sobre a união das distribuições
/// das duas ofertas, e as modalidades, por opção: <see cref="Modalidades"/> é a da 1ª e
/// <see cref="ModalidadesSegundaOpcao"/> a da 2ª — nula quando a inscrição não declara a 2ª.
/// </remarks>
public sealed record ResultadoDaAvaliacaoDaInscricao(
    AvaliacaoFormulario Formulario,
    ModalidadesDeConcorrencia Modalidades,
    DocumentosDaInscricao Documentos,
    ModalidadesDeConcorrencia? ModalidadesSegundaOpcao = null);

/// <summary>
/// Os documentos que o edital exige do candidato (UNI-REQ-0069, UNI-REQ-0077): cada regra de documento com
/// a situação dela diante das respostas e dos documentos enviados, e, por fase, o que falta responder
/// para saber se as exigências daquela fase que podem ser exigidas se aplicam. A fase sem pendência não
/// tem entrada. <see cref="Ausentes"/> são os documentos obrigatórios da inscrição que ainda faltam,
/// decididos pela situação da regra de comprovação (UNI-REQ-0028).
/// </summary>
public sealed record DocumentosDaInscricao(
    IReadOnlyList<DocumentoAvaliado> Itens,
    IReadOnlyDictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>> PendenciasPorFase,
    IReadOnlyList<DocumentoAusente> Ausentes)
{
    /// <summary>Cada ausência obrigatória recusa o envio; arquivo ainda em verificação pede espera (UNI-REQ-0028).</summary>
    public IEnumerable<FieldError> RecusasDosAusentes(IReadOnlySet<(Guid ExigenciaId, string? OcorrenciaId)>? emVerificacao = null) =>
        Ausentes.Select(ausente => RecusaDoAusente(ausente, emVerificacao));

    private static FieldError RecusaDoAusente(DocumentoAusente ausente, IReadOnlySet<(Guid ExigenciaId, string? OcorrenciaId)>? emVerificacao)
    {
        string campo = $"documentos.{ausente.Alternativas[0].Id}" + (ausente.OcorrenciaId is { } ocorrencia ? $".{ocorrencia}" : string.Empty);
        return new FieldError(campo, emVerificacao is not null && ausente.Alternativas.Any(d => emVerificacao.Contains((d.Id, ausente.OcorrenciaId)))
            ? new DomainError(InscricaoErrorCodes.DocumentoObrigatorioEmVerificacao,
                "Aguarde a verificação completa dos documentos obrigatórios para enviar a inscrição.")
            : RecusaDaFalta(ausente));
    }

    private static DomainError RecusaDaFalta(DocumentoAusente ausente)
    {
        string nomes = string.Join(" ou ", ausente.Alternativas.Select(static d => d.TipoDocumentoNome));
        return new DomainError(InscricaoErrorCodes.DocumentoObrigatorioAusente, ausente.Alternativas.Count == 1
            ? $"Envie o documento obrigatório: {nomes}."
            : $"Envie os documentos obrigatórios conforme o edital: {nomes}.");
    }
}

/// <summary>
/// Um documento obrigatório que falta: a exigência, ou as alternativas de um grupo "um ou outro" de que
/// nenhuma foi atendida, e a ocorrência do grupo repetível a que a falta se refere.
/// </summary>
public sealed record DocumentoAusente(IReadOnlyList<DocumentoExigido> Alternativas, string? OcorrenciaId);

/// <summary>
/// As modalidades em que o candidato concorre (UNI-REQ-0076). <see cref="Derivada"/> diz se a
/// configuração deriva a modalidade; sem derivação não há situação, item, pendência nem dependência.
/// <see cref="Dependencias"/> são os campos e os grupos da inscrição de que a modalidade depende: é
/// neles que o candidato confere as respostas quando não concorre a nenhuma.
/// </summary>
public sealed record ModalidadesDeConcorrencia(
    SituacaoDasModalidades? Situacao,
    IReadOnlyList<ModalidadeDeConcorrencia> Itens,
    IReadOnlyList<PendenciaDaAvaliacao> Pendencias,
    DependenciasDaAvaliacao Dependencias)
{
    public bool Derivada => Situacao.HasValue;

    public static ModalidadesDeConcorrencia SemDerivacao { get; } = new(null, [], [], new DependenciasDaAvaliacao([], []));
}

/// <summary>Uma modalidade em que o candidato concorre: o código, o nome congelado e a posição na ordem das ofertadas.</summary>
public sealed record ModalidadeDeConcorrencia(string Codigo, string Nome, int Ordem);

/// <summary>
/// Uma resposta que falta para fechar a avaliação: o campo — ou o grupo — que o formulário pergunta,
/// com o rótulo e a seção em que ele está.
/// </summary>
public sealed record PendenciaDaAvaliacao(string FatoCodigo, string Rotulo, string? SecaoCodigo);

/// <summary>
/// O documento diante do candidato, sua situação e os grupos de comprovação que o contêm.
/// A dispensa indica que um grupo atendido não precisa desse documento para ser cumprido.
/// </summary>
public sealed record DocumentoAvaliado(
    DocumentoExigido Documento,
    SituacaoDoDocumento Situacao,
    string? GrupoCodigo,
    string? OcorrenciaId,
    IReadOnlyList<GrupoDeComprovacao> Grupos,
    string? GrupoDoFormularioParaRepeticao,
    int QuantidadeMinima = RegraDeComprovacao.QuantidadeMinimaPadrao,
    bool DispensadoPeloGrupo = false);

/// <summary>
/// Grupo que exige todos os itens ou uma quantidade mínima, com repetição e consequência quando declaradas.
/// </summary>
public sealed record GrupoDeComprovacao(Guid GrupoId, FormaDeComprovacao Forma, int? Minimo, string? GrupoDoFormularioParaRepeticao, string? Consequencia = null);

/// <summary>
/// Os códigos que o formulário pergunta e de que a avaliação depende: os campos — os subitens de grupo
/// como <c>GRUPO.FATO</c> — e os grupos cujas ocorrências mudam a avaliação. Só códigos, nunca regra.
/// </summary>
public sealed record DependenciasDaAvaliacao(IReadOnlyList<string> Fatos, IReadOnlyList<string> Grupos);
