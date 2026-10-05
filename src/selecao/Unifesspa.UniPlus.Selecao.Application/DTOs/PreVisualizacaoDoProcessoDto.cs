namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

/// <summary>
/// O perfil simulado de um candidato para a pré-visualização do processo: as respostas dos
/// formulários, por fato; as ocorrências dos grupos repetíveis; as etapas dadas como concluídas; e os
/// pressupostos, os fatos que o formulário não coleta e a execução resolve fora dele — o grupo da
/// convocação, a faixa etária, a UF e o município de residência. Quando um pressuposto repete um
/// fato do formulário, vale a resposta do formulário.
/// </summary>
public sealed record PreVisualizacaoDoProcessoInput(
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaSimuladaInput>>? Grupos,
    IReadOnlyList<EtapaConcluidaInput>? EtapasConcluidas,
    IReadOnlyDictionary<string, JsonElement>? Pressupostos);

/// <summary>Uma ocorrência de grupo repetível, pela identidade dela, e as respostas dos campos.</summary>
public sealed record OcorrenciaSimuladaInput(string? Id, IReadOnlyDictionary<string, JsonElement>? Respostas);

/// <summary>Uma seção dada como concluída, no formulário da finalidade.</summary>
public sealed record EtapaConcluidaInput(string Finalidade, string Etapa);

/// <summary>
/// O processo avaliado contra o perfil simulado: o que cada formulário mostra e exige, e os
/// documentos que a árvore de exigências pediria. Os estados são <c>VERDADEIRO</c>, <c>FALSO</c>
/// ou <c>INDETERMINADO</c>, quando dependem de resposta ainda não dada.
/// </summary>
public sealed record PreVisualizacaoDoProcessoDto(
    IReadOnlyList<FormularioSimuladoDto> Formularios,
    IReadOnlyList<DocumentoSimuladoDto> Documentos);

/// <summary>Um formulário avaliado: os itens, os grupos e os termos dele.</summary>
public sealed record FormularioSimuladoDto(
    string Finalidade,
    IReadOnlyList<ItemSimuladoDto> Itens,
    IReadOnlyList<GrupoSimuladoDto> Grupos,
    IReadOnlyList<TermoSimuladoDto> Termos);

/// <summary>
/// Um item avaliado: se aparece, se é obrigatório, os tipos das restrições que a resposta viola e se
/// a resposta impede a inscrição, com a mensagem ao candidato quando o item tem impedimento.
/// </summary>
public sealed record ItemSimuladoDto(
    string FatoCodigo,
    string? EtapaCodigo,
    string Visivel,
    string Obrigatorio,
    IReadOnlyList<string> RestricoesVioladas,
    string Impedido,
    string? MensagemDoImpedimento);

/// <summary>
/// Um grupo repetível avaliado: se aparece e é obrigatório, se a contagem de ocorrências e a
/// ocorrência do próprio candidato valem, e cada ocorrência com os campos dela.
/// </summary>
public sealed record GrupoSimuladoDto(
    string Codigo,
    string? EtapaCodigo,
    string Visivel,
    string Obrigatorio,
    bool ContagemValida,
    bool OcorrenciaDoCandidatoValida,
    IReadOnlyList<OcorrenciaSimuladaDto> Ocorrencias);

/// <summary>Uma ocorrência avaliada e os campos dela.</summary>
public sealed record OcorrenciaSimuladaDto(string Id, IReadOnlyList<ItemSimuladoDto> Itens);

/// <summary>Um termo avaliado: se aparece e se é obrigatório.</summary>
public sealed record TermoSimuladoDto(string Codigo, string Visivel, string Obrigatorio);

/// <summary>
/// Um documento da árvore de exigências diante do perfil: <c>EXIGIDO</c>, <c>NAO_EXIGIDO</c> ou
/// <c>INDETERMINADO</c>, quando a aplicabilidade depende de resposta ainda não dada. O documento de
/// um grupo repetível aparece uma vez por ocorrência, com a identidade dela; o que está dentro de
/// grupos de alternativas traz cada um deles, do mais externo ao mais interno, com o mínimo que pede.
/// </summary>
/// <param name="Finalidade">
/// O formulário em que o documento é apresentado (<c>INSCRICAO</c>, <c>ISENCAO_TAXA</c> ou
/// <c>HABILITACAO</c>); nulo quando a fase não responde formulário nenhum. Inscrição e isenção podem
/// dividir a fase, e é a finalidade que separa os documentos de cada formulário.
/// </param>
public sealed record DocumentoSimuladoDto(
    Guid ExigenciaId,
    string TipoDocumentoCodigo,
    string TipoDocumentoNome,
    bool Obrigatorio,
    Guid FaseId,
    string? Finalidade,
    Guid? EtapaId,
    string Situacao,
    string? EntidadeId,
    IReadOnlyList<AlternativasSimuladasDto> Alternativas);

/// <summary>Um grupo de alternativas em que o documento está e quantas alternativas ele pede.</summary>
public sealed record AlternativasSimuladasDto(Guid GrupoId, int Minimo);
