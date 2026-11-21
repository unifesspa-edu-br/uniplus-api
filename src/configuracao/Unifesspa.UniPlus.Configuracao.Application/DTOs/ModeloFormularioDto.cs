namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// Um modelo de formulário na leitura da manutenção, com o mesmo formato da escrita: o
/// <see cref="Descritivo"/> e o <see cref="Conteudo"/> voltam ao PUT sem transformação. O identificador, o
/// código, a finalidade e o estado ativo são do modelo e o PUT os ignora.
/// </summary>
/// <param name="Finalidade">Token canônico: INSCRICAO, ISENCAO_TAXA ou HABILITACAO.</param>
/// <param name="Ativo">O modelo desativado sai da escolha dos processos novos; o processo que já o copiou não muda.</param>
public sealed record ModeloFormularioDto(
    Guid Id,
    string Codigo,
    string Finalidade,
    bool Ativo,
    DescritivoDoModeloDto Descritivo,
    ConteudoDoModeloDto Conteudo);

/// <summary>O nome, a descrição e o tipo de processo a que o modelo se destina; o tipo é nulo quando o modelo serve a todos.</summary>
public sealed record DescritivoDoModeloDto(string Nome, string? Descricao, string? TipoProcessoCodigo);

/// <summary>
/// O conteúdo do modelo: o título, as etapas, os itens, os termos, os fatos pressupostos — coletados pela
/// inscrição, citáveis pelo modelo de outra finalidade (UNI-REQ-0144) —, os grupos repetíveis (UNI-REQ-0146)
/// e os documentos exigidos.
/// </summary>
public sealed record ConteudoDoModeloDto(
    string? Titulo,
    IReadOnlyList<EtapaFormularioDto> Etapas,
    IReadOnlyList<ItemDoModeloDto> Itens,
    IReadOnlyList<TermoDoModeloDto> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<GrupoDoModeloDto> Grupos,
    IReadOnlyList<DocumentoExigidoDoModeloDto> DocumentosExigidos);

/// <summary>Um campo do modelo: o fato que ele coleta, a posição, a seção, a apresentação e as regras.</summary>
public sealed record ItemDoModeloDto(
    string FatoCodigo,
    int Ordem,
    string? EtapaCodigo,
    ApresentacaoDoCampoDto Apresentacao,
    RegrasDoCampoDto Regras);

/// <summary>Um grupo repetível do modelo, com as ocorrências admitidas, a exibição e a obrigatoriedade em <see cref="Regras"/> e os campos de cada ocorrência.</summary>
public sealed record GrupoDoModeloDto(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    OcorrenciasDoGrupoDto Ocorrencias,
    ExibicaoEObrigatoriedadeDto Regras,
    IReadOnlyList<ItemDoModeloDto> Subitens);

/// <summary>Um termo que o modelo exige: a posição, a versão do catálogo escolhida e as regras de exibição e de obrigatoriedade.</summary>
public sealed record TermoDoModeloDto(string Codigo, int Ordem, VersaoDoTermoDoModeloDto Termo, ExibicaoEObrigatoriedadeDto Regras);

/// <summary>A versão do catálogo de termos que o modelo escolhe: o termo e a versão dele.</summary>
public sealed record VersaoDoTermoDoModeloDto(Guid TermoId, Guid VersaoId);

/// <summary>
/// Um documento do conjunto padrão do modelo: a fase e o tipo de documento pelo código, as condições de
/// exigência — nulas quando vale para todo candidato —, a consequência do indeferimento, as bases legais e,
/// quando há, a exigência por opção.
/// </summary>
public sealed record DocumentoExigidoDoModeloDto(
    string FaseCodigo,
    string TipoDocumentoCodigo,
    bool Obrigatorio,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Quando,
    string ConsequenciaIndeferimento,
    IReadOnlyList<BaseLegalDoModeloDto> BasesLegais,
    ExigenciaPorOpcaoDto? PorOpcao);

/// <summary>Uma base legal do documento exigido do modelo, com abrangência e status em token canônico.</summary>
public sealed record BaseLegalDoModeloDto(string Referencia, string Abrangencia, string Status, string? Observacao);

/// <summary>O documento exigido uma vez por opção que o processo oferece ao fato, salvo as listadas em <see cref="Exceto"/>.</summary>
public sealed record ExigenciaPorOpcaoDto(string FatoCodigo, IReadOnlyList<string> Exceto);
