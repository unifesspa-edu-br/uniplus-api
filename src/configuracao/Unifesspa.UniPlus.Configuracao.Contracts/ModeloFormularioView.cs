namespace Unifesspa.UniPlus.Configuracao.Contracts;

using Unifesspa.UniPlus.Regras.Entradas;

/// <summary>
/// O conteúdo do modelo no mesmo formato da escrita do formulário do processo: título, etapas,
/// itens, termos, os fatos pressupostos — coletados pela inscrição, citáveis pelo modelo de outra
/// finalidade (UNI-REQ-0144) — e os grupos repetíveis (UNI-REQ-0146); grupos ausentes são nenhum.
/// </summary>
public sealed record ConteudoDoModeloInput(
    string? Titulo,
    IReadOnlyList<EtapaFormularioInput>? Etapas,
    IReadOnlyList<FatoColetadoInput>? Itens,
    IReadOnlyList<TermoExigidoInput>? Termos,
    IReadOnlyList<string>? Pressupostos,
    IReadOnlyList<GrupoColetadoInput>? Grupos = null,
    IReadOnlyList<DocumentoExigidoDoModeloInput>? DocumentosExigidos = null);

/// <summary>Uma base legal do documento exigido do modelo; tokens de abrangência e status como os da exigência do processo.</summary>
public sealed record BaseLegalDoModeloInput(string Referencia, string Abrangencia, string Status, string? Observacao);

/// <summary>
/// Um documento do conjunto padrão do modelo: tipo do catálogo e fase canônica pelo código,
/// condições sobre fatos no formato das exibições (ausentes, vale para todo candidato) e consequência
/// do indeferimento. A aplicação do modelo copia o conjunto para os documentos exigidos do processo.
/// </summary>
public sealed record DocumentoExigidoDoModeloInput(
    string FaseCodigo,
    string TipoDocumentoCodigo,
    bool Obrigatorio,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Quando,
    string ConsequenciaIndeferimento,
    IReadOnlyList<BaseLegalDoModeloInput?>? BasesLegais);

/// <summary>
/// Um modelo de formulário composto pelo administrador para um tipo de processo — ou para todos —
/// e uma finalidade (UNI-REQ-0144). O conteúdo está no formato da escrita do formulário do processo:
/// a cópia para o processo passa pelas mesmas conferências que a escrita direta.
/// </summary>
/// <param name="Finalidade">Token canônico: INSCRICAO, ISENCAO_TAXA ou HABILITACAO.</param>
/// <param name="TipoProcessoCodigo">O tipo de processo a que o modelo se destina; nulo quando serve a todos.</param>
/// <param name="Ativo">O modelo desativado sai da escolha dos processos novos; o processo que já o copiou não muda.</param>
public sealed record ModeloFormularioView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string Finalidade,
    string? TipoProcessoCodigo,
    bool Ativo,
    ConteudoDoModeloInput Conteudo);
