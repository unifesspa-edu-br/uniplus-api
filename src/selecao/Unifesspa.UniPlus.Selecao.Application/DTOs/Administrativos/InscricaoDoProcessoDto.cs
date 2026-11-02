namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Uma inscrição na listagem de quem opera o processo. Cada campo é opcional e sai pela permissão da sua
/// classificação: o que a pessoa não pode ver não vem, sem indicação de que foi omitido (ADR-0153). Sem documento e
/// sem o CPF completo.
/// </summary>
/// <param name="Id">A identidade da inscrição.</param>
/// <param name="NumeroInscricao">O número gerado no primeiro envio.</param>
/// <param name="Situacao">Submetida ou cancelada.</param>
/// <param name="OfertaCursoId">A oferta de curso da primeira opção.</param>
/// <param name="OfertaCursoSegundaOpcaoId">A oferta de curso da segunda opção, quando há.</param>
/// <param name="Nome">O nome exibido, como item classificado pela proteção que as versões do processo congelaram para ele.</param>
/// <param name="Cpf">O CPF no padrão <c>***.999.999-**</c>; o completo só se revela pela permissão própria, no detalhe.</param>
/// <param name="SubmetidaEm">O instante do primeiro envio.</param>
public sealed record InscricaoDoProcessoDto(
    [property: ProtegidoComo(ClassificacaoDeProtecao.Publico, Fonte = "selecao.inscricoes.id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? Id,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Interno, Fonte = "selecao.inscricoes.numero_inscricao")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? NumeroInscricao,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Publico, Fonte = "selecao.inscricoes.situacao")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    SituacaoInscricao? Situacao,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Publico, Fonte = "selecao.inscricoes.oferta_curso_id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? OfertaCursoId,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Publico, Fonte = "selecao.inscricoes.oferta_curso_segunda_opcao_id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? OfertaCursoSegundaOpcaoId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    NomeClassificadoDto? Nome,
    [property: ProtegidoComo(
        ClassificacaoDeProtecao.Identificador,
        Fonte = "selecao.respostas_da_inscricao.valor_cifrado",
        Transformacao = TransformacoesDeProtecao.MascaraDoCpf)]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CpfMascaradoDto? Cpf,
    [property: ProtegidoComo(ClassificacaoDeProtecao.Interno, Fonte = "selecao.comprovantes_inscricao.instante")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? SubmetidaEm);

/// <summary>O CPF mascarado de uma inscrição; a revelação do valor completo é pedida por campo, no detalhe.</summary>
/// <param name="Valor">O CPF no padrão <c>***.999.999-**</c>.</param>
/// <param name="PodeRevelar">Se a pessoa pode revelar o CPF; na listagem é sempre falso, porque a revelação é no detalhe.</param>
public sealed record CpfMascaradoDto(string Valor, bool PodeRevelar);
