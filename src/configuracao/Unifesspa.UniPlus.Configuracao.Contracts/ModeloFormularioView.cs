namespace Unifesspa.UniPlus.Configuracao.Contracts;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O conteúdo do modelo no mesmo formato da escrita do formulário do processo: título, páginas,
/// itens, termos, os fatos pressupostos — coletados pela inscrição, citáveis pelo modelo de outra
/// fase (UNI-REQ-0144) — e os grupos repetíveis (UNI-REQ-0146); grupos ausentes são nenhum.
/// </summary>
public sealed record ConteudoDoModeloInput(
    string? Titulo,
    IReadOnlyList<PaginaDoFormularioInput>? Paginas,
    IReadOnlyList<FatoColetadoInput>? Itens,
    IReadOnlyList<TermoExigidoInput>? Termos,
    IReadOnlyList<string>? Pressupostos,
    IReadOnlyList<GrupoColetadoInput>? Grupos = null,
    ConjuntoDeDocumentosDoModeloInput? ConjuntoDeDocumentos = null);

/// <summary>
/// Um modelo de formulário composto pelo administrador para um tipo de processo — ou para todos —, que
/// declara se serve à fase de inscrição e se é obrigatório (UNI-REQ-0144). O conteúdo está no formato da escrita do formulário do processo:
/// a cópia para o processo passa pelas mesmas conferências que a escrita direta.
/// </summary>
/// <param name="Declaracao">Se o modelo serve à fase de inscrição e se é obrigatório, para a tela aplicar o modelo sobre o documento do processo.</param>
/// <param name="TipoProcessoCodigo">O tipo de processo a que o modelo se destina; nulo quando serve a todos.</param>
/// <param name="Ativo">O modelo desativado sai da escolha dos processos novos; o processo que já o copiou não muda.</param>
public sealed record ModeloFormularioView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    DeclaracaoDoFormulario Declaracao,
    string? TipoProcessoCodigo,
    bool Ativo,
    ConteudoDoModeloInput Conteudo);
