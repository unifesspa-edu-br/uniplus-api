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
/// seção do que falta responder, as modalidades que a distribuição de vagas oferta, com o nome
/// congelado de cada uma, e as fases e os formulários, que dizem em que fase cada finalidade pergunta.
/// </summary>
public sealed record ConfiguracaoAvaliavel(
    DefinicaoFormulario Definicao,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas,
    ArvoreExigenciasCongelada Arvore,
    IReadOnlyCollection<FatoColetado> Campos,
    IReadOnlyCollection<GrupoColetado> Grupos,
    IReadOnlyCollection<ModalidadeSelecionada> ModalidadesOfertadas,
    IReadOnlyCollection<FaseCronograma> Fases,
    IReadOnlyCollection<FormularioProcesso> Formularios);

/// <summary>
/// O que o candidato respondeu e o que se sabe dele de fora do formulário: as respostas e os grupos;
/// os fatos já conhecidos — os pressupostos e os derivados do sistema que se conseguiu apurar —; e as
/// etapas dadas como concluídas, que decidem se o opcional em branco é não informado; e o instante
/// em que as respostas são gravadas, de que dependem os limites de data relativos a ele — nulo na
/// pré-visualização, que não grava. <see cref="NaFaseDaInscricao"/> diz que quem avalia é o candidato,
/// na fase da inscrição: o que só se conhece numa fase posterior não decide documento nenhum. A
/// pré-visualização simula o perfil inteiro, e por isso não o diz.
/// </summary>
public sealed record EntradaDaAvaliacaoDaInscricao(
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRespondida>> Grupos,
    IReadOnlyDictionary<string, FatoResolvido> FatosConhecidos,
    IReadOnlySet<string> EtapasConcluidas,
    EnvioDasRespostas? Envio,
    bool NaFaseDaInscricao = false);

/// <summary>
/// O resultado de uma avaliação só: a do formulário, a de cada exigência da árvore diante do candidato
/// e as modalidades em que ele concorre.
/// </summary>
public sealed record ResultadoDaAvaliacaoDaInscricao(
    AvaliacaoFormulario Formulario,
    ModalidadesDeConcorrencia Modalidades,
    DocumentosDaInscricao Documentos);

/// <summary>
/// Os documentos que o edital exige do candidato (UNI-REQ-0069, UNI-REQ-0077): cada folha da árvore com
/// a situação dela diante das respostas, e, por fase, o que falta responder para saber se as exigências
/// daquela fase que podem ser exigidas se aplicam. A fase sem pendência não tem entrada.
/// </summary>
public sealed record DocumentosDaInscricao(
    IReadOnlyList<DocumentoAvaliado> Itens,
    IReadOnlyDictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>> PendenciasPorFase);

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
/// Uma folha da árvore de exigências diante do candidato: a exigência, a situação, o grupo de membros a
/// que ela se liga e a ocorrência dele — a ocorrência é nula quando ainda não se sabe quais membros o
/// grupo tem —, os grupos da árvore que a contêm, do mais externo ao mais interno, e o grupo pelo qual a
/// própria folha se repete, quando é nela que a repetição começa. Pelos grupos se reconstrói a escolha
/// inteira: o E sob um OU é uma alternativa só, e a repetição vale do nó que a declara para baixo.
/// </summary>
public sealed record DocumentoAvaliado(
    DocumentoExigido Documento,
    SituacaoDoDocumento Situacao,
    string? GrupoCodigo,
    string? OcorrenciaId,
    IReadOnlyList<GrupoDaArvore> Grupos,
    string? RepetePorEntidade);

/// <summary>
/// Um grupo da árvore de exigências: o nó, se é um E ou um OU, no OU quantos dos filhos bastam — o N-de
/// é o OU com mínimo maior que um; no E todos os filhos são exigidos, e o mínimo é nulo —, e o grupo de
/// membros pelo qual o nó se repete, quando a repetição começa nele: a escolha vale então para cada
/// membro, e não para o candidato.
/// </summary>
public sealed record GrupoDaArvore(Guid GrupoId, TipoNo Tipo, int? Minimo, string? RepetePorEntidade);

/// <summary>
/// Os códigos que o formulário pergunta e de que a avaliação depende: os campos — os subitens de grupo
/// como <c>GRUPO.FATO</c> — e os grupos cujas ocorrências mudam a avaliação. Só códigos, nunca regra.
/// </summary>
public sealed record DependenciasDaAvaliacao(IReadOnlyList<string> Fatos, IReadOnlyList<string> Grupos);
