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
    IReadOnlyList<GrupoColetadoInput>? Grupos = null);

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
