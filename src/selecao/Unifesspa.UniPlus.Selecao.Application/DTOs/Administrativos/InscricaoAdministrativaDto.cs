namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;

using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A inscrição na consulta de quem opera o processo, projetada pela permissão da classe de cada dado e de cada
/// arquivo (ADR-0153). Os campos fixos da entidade são opcionais e omitidos quando a pessoa não os pode ver; as respostas
/// do formulário são itens todos da mesma forma, e o item que a pessoa não pode ver não vem, sem indicação nem contagem.
/// </summary>
/// <param name="Id">A identidade da inscrição: sai sempre que o acesso à inscrição foi concedido, porque é com ela que se abre o detalhe.</param>
/// <param name="Situacao">Rascunho, submetida ou cancelada.</param>
/// <param name="Processo">O processo seletivo da inscrição.</param>
/// <param name="Ofertas">As ofertas que o candidato escolheu, na ordem da escolha, nomeadas pela versão do formulário da inscrição.</param>
/// <param name="NumeroInscricao">O número gerado no primeiro envio.</param>
/// <param name="SubmetidaEm">O instante do primeiro envio.</param>
/// <param name="CanceladaEm">O instante do cancelamento, quando cancelada.</param>
/// <param name="MotivoDoCancelamento">O motivo informado no cancelamento.</param>
/// <param name="Secoes">As respostas do formulário agrupadas por seção, na ordem de coleta.</param>
/// <param name="Derivados">O que o sistema calculou das respostas.</param>
/// <param name="Arquivos">Os arquivos enviados, só os metadados: abrir o arquivo é outra operação, com a permissão da classe dele.</param>
/// <param name="PodeEmitirComprovante">Se a pessoa tem a permissão de emitir o comprovante desta inscrição; a mesma que a emissão exige, de modo que a tela não decide por conta própria. Vem da consulta de capacidade, que não grava registro.</param>
public sealed record InscricaoAdministrativaDto(
    Guid Id,
    ProcessoDaInscricaoDto Processo,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Publico, Fonte = "selecao.inscricoes.situacao")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    SituacaoInscricao? Situacao,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Publico, Fonte = "selecao.inscricoes.oferta_curso_id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OfertaEscolhidaDto>? Ofertas,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Interno, Fonte = "selecao.inscricoes.numero_inscricao")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? NumeroInscricao,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Interno, Fonte = "selecao.comprovantes_inscricao.instante")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? SubmetidaEm,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Interno, Fonte = "selecao.inscricoes.cancelada_em")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? CanceladaEm,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Pessoal, Fonte = "selecao.inscricoes.motivo_do_cancelamento")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? MotivoDoCancelamento,
    IReadOnlyList<SecaoDaInscricaoDto> Secoes,
    IReadOnlyList<ItemDeRespostaDto> Derivados,
    IReadOnlyList<ArquivoAdministrativoDto> Arquivos,
    bool PodeEmitirComprovante);

/// <summary>Uma seção do formulário com as respostas dela e os grupos que se repetem nela.</summary>
/// <param name="Codigo">O código da seção do formulário; vazio para o item que nenhuma seção abriga.</param>
/// <param name="Titulo">O título da seção no formulário que o processo congelou, o mesmo que o candidato vê; ausente quando a seção não existe na versão congelada, quando o envelope não traz formulário e para o item que nenhuma seção abriga.</param>
/// <param name="Itens">As respostas da seção fora de grupo.</param>
/// <param name="Grupos">Os grupos repetíveis da seção.</param>
public sealed record SecaoDaInscricaoDto(
    string Codigo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Titulo,
    IReadOnlyList<ItemDeRespostaDto> Itens,
    IReadOnlyList<GrupoDaInscricaoDto> Grupos);

/// <summary>Um grupo repetível com as ocorrências que a inscrição respondeu.</summary>
/// <param name="Codigo">O código do grupo.</param>
/// <param name="Rotulo">O rótulo do grupo, congelado no processo.</param>
/// <param name="Ocorrencias">As ocorrências, na ordem em que foram respondidas.</param>
public sealed record GrupoDaInscricaoDto(string Codigo, string Rotulo, IReadOnlyList<OcorrenciaDaInscricaoDto> Ocorrencias);

/// <summary>Uma ocorrência de grupo, pelo identificador estável dela.</summary>
/// <param name="Id">O identificador da ocorrência, que a revelação de um campo do grupo cita.</param>
/// <param name="Itens">As respostas da ocorrência.</param>
public sealed record OcorrenciaDaInscricaoDto(string Id, IReadOnlyList<ItemDeRespostaDto> Itens);

/// <summary>
/// Uma resposta do formulário, todas com a mesma forma. O <c>tipo</c> diz como ler o <c>valor</c> e o <c>componente</c>, como
/// exibi-lo; os dois, o rótulo e a descrição vêm do que o processo congelou na publicação, nunca do catálogo vivo. O
/// identificador sai mascarado, e o valor completo só se pede por <c>revelacao</c>.
/// </summary>
public sealed record ItemDeRespostaDto : ItemClassificado
{
    /// <summary>Cria o item com a classificação e a base legal com que a saída é decidida.</summary>
    public ItemDeRespostaDto(ClassificacaoDeProtecao classificacao, string? baseLegal)
        : base(NaturezaDoItem.Dado, classificacao, baseLegal)
    {
    }

    /// <summary>O código do fato no vocabulário do candidato.</summary>
    public required string FatoCodigo { get; init; }

    /// <summary>O rótulo do campo no formulário do processo.</summary>
    public required string Rotulo { get; init; }

    /// <summary>O que o dado significa, para a dica e o leiaute da tela.</summary>
    public required string Descricao { get; init; }

    /// <summary>Como ler o valor: texto, data, data-hora, hora, booleano, numero, opcao, opcoes ou endereco.</summary>
    public required string Tipo { get; init; }

    /// <summary>Como exibir: padrao, cpf, identificador, moeda ou situacao.</summary>
    public required string Componente { get; init; }

    /// <summary>
    /// O valor, na forma que o <see cref="Tipo"/> define.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Valor { get; init; }

    /// <summary>
    /// Se a pessoa pode revelar o valor completo do identificador; falso nos demais itens. Vem da consulta de capacidade,
    /// que não grava registro.
    /// </summary>
    public required bool PodeRevelar { get; init; }
}

/// <summary>
/// Um arquivo enviado, só os metadados: nunca a chave do objeto, a URL do armazenamento nem o nome original. Abrir o
/// arquivo é outra operação, com a permissão da classe da exigência a que ele responde.
/// </summary>
public sealed record ArquivoAdministrativoDto : ItemClassificado
{
    /// <summary>Cria o arquivo com a classificação e a base legal da exigência congelada.</summary>
    public ArquivoAdministrativoDto(ClassificacaoDeProtecao classificacao, string? baseLegal)
        : base(NaturezaDoItem.Arquivo, classificacao, baseLegal)
    {
    }

    /// <summary>A identidade do arquivo.</summary>
    public required Guid ArquivoId { get; init; }

    /// <summary>A exigência a que o arquivo responde.</summary>
    public required Guid ExigenciaId { get; init; }

    /// <summary>A ocorrência do grupo que repete a exigência, quando há.</summary>
    public string? OcorrenciaId { get; init; }

    /// <summary>O nome do documento exigido, congelado na publicação.</summary>
    public required string NomeDoDocumento { get; init; }

    /// <summary>O tipo de mídia do arquivo.</summary>
    public required string TipoDeMidia { get; init; }

    /// <summary>
    /// A proteção congelada na exigência (<c>PUBLICO</c>, <c>INTERNO</c>, <c>PESSOAL</c>, <c>IDENTIFICADOR</c> ou
    /// <c>SENSIVEL</c>); a classificação que não se resolve vale como <c>SENSIVEL</c>, a mesma regra que decide a saída do arquivo.
    /// </summary>
    [TokenDeClassificacao]
    [JsonPropertyName("classificacaoProtecao")]
    public string ClassificacaoProtecao => ClassificacoesDeProtecao.ParaTokenCanonico(ClassificacoesDeProtecao.Resolvida(Classificacao));

    /// <summary>A situação da verificação do arquivo.</summary>
    public required SituacaoDoArquivoDoCandidato Situacao { get; init; }

    /// <summary>O tamanho em bytes; só existe no arquivo verificado.</summary>
    public long? TamanhoBytes { get; init; }

    /// <summary>O SHA-256 do conteúdo; só existe no arquivo verificado.</summary>
    public string? HashSha256 { get; init; }

    /// <summary>O instante em que o envio foi confirmado.</summary>
    public required DateTimeOffset ConfirmadoEm { get; init; }
}
