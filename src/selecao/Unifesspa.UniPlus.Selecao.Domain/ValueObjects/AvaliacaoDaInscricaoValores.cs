namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Entities;

using Enums;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A configuração do processo que a avaliação da inscrição lê — a congelada na versão que a inscrição
/// fixou (UNI-REQ-0084) ou, na pré-visualização, a viva do rascunho: a definição avaliável inteira, a
/// oferta de valores de cada campo, a árvore de exigências, os campos e os grupos que dão o rótulo e a
/// seção do que falta responder, e as modalidades que a distribuição de vagas oferta, com o nome
/// congelado de cada uma.
/// </summary>
public sealed record ConfiguracaoAvaliavel(
    DefinicaoFormulario Definicao,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas,
    ArvoreExigenciasCongelada Arvore,
    IReadOnlyCollection<FatoColetado> Campos,
    IReadOnlyCollection<GrupoColetado> Grupos,
    IReadOnlyCollection<ModalidadeSelecionada> ModalidadesOfertadas);

/// <summary>
/// O que o candidato respondeu e o que se sabe dele de fora do formulário: as respostas e os grupos;
/// os fatos já conhecidos — os pressupostos e os derivados do sistema que se conseguiu apurar —; e as
/// etapas dadas como concluídas, que decidem se o opcional em branco é não informado; e o instante
/// em que as respostas são gravadas, de que dependem os limites de data relativos a ele — nulo na
/// pré-visualização, que não grava.
/// </summary>
public sealed record EntradaDaAvaliacaoDaInscricao(
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRespondida>> Grupos,
    IReadOnlyDictionary<string, FatoResolvido> FatosConhecidos,
    IReadOnlySet<string> EtapasConcluidas,
    EnvioDasRespostas? Envio);

/// <summary>
/// O resultado de uma avaliação só: a do formulário, a de cada exigência da árvore diante do candidato
/// e as modalidades em que ele concorre.
/// </summary>
public sealed record ResultadoDaAvaliacaoDaInscricao(
    AvaliacaoFormulario Formulario,
    ModalidadesDeConcorrencia Modalidades,
    IReadOnlyList<DocumentoAvaliado> Documentos);

/// <summary>
/// As modalidades em que o candidato concorre (UNI-REQ-0076). <see cref="Derivada"/> diz se a
/// configuração deriva a modalidade; sem derivação não há situação, item, pendência nem dependência.
/// <see cref="Dependencias"/> são os campos e os grupos da inscrição de que a modalidade depende: é
/// neles que o candidato confere as respostas quando não concorre a nenhuma.
/// </summary>
public sealed record ModalidadesDeConcorrencia(
    bool Derivada,
    SituacaoDasModalidades Situacao,
    IReadOnlyList<ModalidadeDeConcorrencia> Itens,
    IReadOnlyList<PendenciaDaAvaliacao> Pendencias,
    DependenciasDaAvaliacao Dependencias)
{
    public static ModalidadesDeConcorrencia SemDerivacao { get; } = new(false, SituacaoDasModalidades.Nenhuma, [], [], new DependenciasDaAvaliacao([], []));
}

/// <summary>Uma modalidade em que o candidato concorre: o código, o nome congelado e a posição na ordem das ofertadas.</summary>
public sealed record ModalidadeDeConcorrencia(string Codigo, string Nome, int Ordem);

/// <summary>
/// Uma resposta que falta para fechar a avaliação: o campo — ou o grupo — que o formulário pergunta,
/// com o rótulo e a seção em que ele está.
/// </summary>
public sealed record PendenciaDaAvaliacao(string FatoCodigo, string Rotulo, string? SecaoCodigo);

/// <summary>
/// Uma folha da árvore de exigências diante do candidato: a exigência, a situação, a ocorrência do
/// grupo a que ela se liga, quando a subárvore se repete por membro, e os grupos de alternativas que a
/// contêm, do mais externo ao mais interno.
/// </summary>
public sealed record DocumentoAvaliado(
    DocumentoExigido Documento,
    StatusResolucaoExigencia Status,
    string? OcorrenciaId,
    IReadOnlyList<AlternativaDeDocumento> Alternativas);

/// <summary>Um grupo de alternativas da árvore: o nó e quantos dos filhos bastam.</summary>
public sealed record AlternativaDeDocumento(Guid GrupoId, int Minimo);

/// <summary>
/// Os códigos que o formulário pergunta e de que a avaliação depende: os campos — os subitens de grupo
/// como <c>GRUPO.FATO</c> — e os grupos cujas ocorrências mudam a avaliação. Só códigos, nunca regra.
/// </summary>
public sealed record DependenciasDaAvaliacao(IReadOnlyList<string> Fatos, IReadOnlyList<string> Grupos);
