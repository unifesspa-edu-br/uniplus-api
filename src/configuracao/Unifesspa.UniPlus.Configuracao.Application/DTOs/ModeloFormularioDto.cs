namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// Um modelo de formulário na leitura da manutenção, com o mesmo formato da escrita: o
/// <see cref="Descritivo"/> e o <see cref="Conteudo"/> voltam ao PUT trocando cada <c>{ codigo, rotulo }</c>
/// pelo <c>codigo</c>. O identificador, o código, a declaração e o estado ativo são do modelo e o PUT os ignora.
/// </summary>
/// <param name="Declaracao">Se o modelo serve à fase de inscrição e se é obrigatório.</param>
/// <param name="Ativo">O modelo desativado sai da escolha dos processos novos; o processo que já o copiou não muda.</param>
public sealed record ModeloFormularioDto(
    Guid Id,
    string Codigo,
    DeclaracaoDoModeloDto Declaracao,
    bool Ativo,
    DescritivoDoModeloDto Descritivo,
    ConteudoDoModeloDto Conteudo);

/// <summary>
/// O que o modelo declara de si: se serve à fase que coleta a inscrição, caso em que traz o conjunto básico de
/// dados do candidato, e se é obrigatório. Até o formulário do processo deixar de ter finalidade, ele deriva a
/// finalidade dessa declaração ao aplicar o modelo, e o modelo que serve à inscrição é tratado como obrigatório.
/// </summary>
public sealed record DeclaracaoDoModeloDto(bool ServeAFaseDeInscricao, bool Obrigatorio);

/// <summary>O nome, a descrição e o tipo de processo a que o modelo se destina; o tipo é nulo quando o modelo serve a todos.</summary>
public sealed record DescritivoDoModeloDto(string Nome, string? Descricao, string? TipoProcessoCodigo);

/// <summary>
/// O conteúdo do modelo: o título, as páginas, os itens, os termos, os fatos pressupostos — coletados pela
/// inscrição, citáveis pelo modelo de outra fase (UNI-REQ-0144) —, os grupos repetíveis (UNI-REQ-0146)
/// e o conjunto de documentos exigidos.
/// </summary>
public sealed record ConteudoDoModeloDto(
    string? Titulo,
    IReadOnlyList<PaginaDoFormularioDto> Paginas,
    IReadOnlyList<ItemDoModeloDto> Itens,
    IReadOnlyList<TermoDoModeloDto> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<GrupoDoModeloDto> Grupos,
    ConjuntoDeDocumentosDoModeloDto ConjuntoDeDocumentos);

/// <summary>Um campo do modelo: o fato que ele coleta, a posição, a seção, a apresentação e as regras.</summary>
public sealed record ItemDoModeloDto(
    string FatoCodigo,
    int Ordem,
    string? PaginaCodigo,
    ApresentacaoDoCampoDto Apresentacao,
    RegrasDoCampoDto Regras);

/// <summary>Um grupo repetível do modelo, com as ocorrências admitidas, a exibição e a obrigatoriedade em <see cref="Regras"/> e os campos de cada ocorrência.</summary>
public sealed record GrupoDoModeloDto(
    string Codigo,
    int Ordem,
    string? PaginaCodigo,
    string Rotulo,
    OcorrenciasDoGrupoDto Ocorrencias,
    ExibicaoEObrigatoriedadeDto Regras,
    IReadOnlyList<ItemDoModeloDto> Subitens);

/// <summary>Um termo que o modelo exige: a posição, a versão do catálogo escolhida e as regras de exibição e de obrigatoriedade.</summary>
public sealed record TermoDoModeloDto(string Codigo, int Ordem, VersaoDoTermoDoModeloDto Termo, ExibicaoEObrigatoriedadeDto Regras);

/// <summary>A versão do catálogo de termos que o modelo escolhe: o termo e a versão dele.</summary>
public sealed record VersaoDoTermoDoModeloDto(Guid TermoId, Guid VersaoId);
