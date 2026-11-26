namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Um fato do catálogo como a manutenção o devolve: o fato e os operadores que uma condição de regra pode usar
/// sobre ele.
/// </summary>
/// <param name="Fato">O fato, com o estado de cada parte e o circuito de aprovação da proteção de dados.</param>
/// <param name="OperadoresAdmitidos">
/// Os operadores que uma condição pode usar sobre o fato, na ordem em que a tela os oferece; o primeiro é o
/// padrão da condição nova. Cada rótulo está no sentido da cardinalidade do fato. Vazia quando o fato não é
/// citável em regra (texto, data, endereço) ou quando não é categórico e tem mais de um valor.
/// </param>
public sealed record FatoDeManutencaoDto(
    FatoCandidatoDto Fato,
    IReadOnlyList<CodigoComRotulo<Operador>> OperadoresAdmitidos);

/// <summary>
/// Representação de manutenção de um fato do catálogo (ADR-0136): o descritivo, a definição — os eixos, os
/// valores de domínio, a resolução e a proteção de dados, com o circuito de aprovação —, as regras padrão do
/// derivado por regra e o estado. Os eixos são apresentados com código e rótulo.
/// </summary>
/// <param name="Id">Identificador do fato.</param>
/// <param name="Codigo">Código do fato, chave natural; imutável.</param>
/// <param name="Descritivo">Como o fato se apresenta; é o corpo de <c>PUT admin/fatos-candidato/{id}</c>.</param>
/// <param name="Definicao">
/// O que o fato é; os campos que o administrador altera são o corpo de <c>PUT admin/fatos-candidato/{id}/definicao</c>.
/// </param>
/// <param name="RegrasPadrao">As regras padrão do derivado por regra; vazia nos demais fatos.</param>
/// <param name="SemeadoPeloSistema"><c>true</c> no fato que o sistema semeou e mantém.</param>
/// <param name="Ativo">Fato desativado não aceita vínculo novo; o processo que já o usa continua com ele.</param>
public sealed record FatoCandidatoDto(
    Guid Id,
    string Codigo,
    DescritivoDoFatoDto Descritivo,
    DefinicaoDoFatoDto Definicao,
    IReadOnlyList<RegraPadraoDto> RegrasPadrao,
    bool SemeadoPeloSistema,
    bool Ativo);

/// <summary>Como o fato se apresenta. No PUT, envie <c>componenteExibicao.codigo</c> como texto em <c>componenteExibicao</c>.</summary>
/// <param name="Nome">Rótulo legível do fato.</param>
/// <param name="Descricao">O que o dado significa, para quem analisa a inscrição.</param>
/// <param name="ComponenteExibicao">Componente de exibição, com código e rótulo, quando o fato declara um.</param>
public sealed record DescritivoDoFatoDto(string Nome, string Descricao, CodigoComRotulo<ComponenteDeExibicao>? ComponenteExibicao);

/// <summary>
/// O que o fato é. Os campos que o administrador altera têm a forma do corpo de <c>PUT admin/fatos-candidato/{id}/definicao</c>:
/// <c>Dominio</c>, <c>Cardinalidade</c>, <c>Escopo</c> e <c>Formato</c> com o mesmo nome; <c>Valores.Fonte</c> é
/// <c>fonteValores</c>; <c>Resolucao.PontoResolucao</c> é <c>pontoResolucao</c>; <c>Protecao.Classificacao</c>,
/// <c>Protecao.Finalidade</c> e <c>Protecao.HipoteseLegal</c> são <c>classificacaoProtecao</c>,
/// <c>finalidadeTratamento</c> e <c>hipoteseLegal</c>. A origem e o vínculo não mudam, e os valores declarados e a
/// aprovação da proteção têm operações próprias. No PUT, envie o <c>codigo</c> de cada campo com código e rótulo como texto no campo correspondente.
/// </summary>
/// <param name="Dominio">O tipo do dado, com código e rótulo.</param>
/// <param name="Cardinalidade">Se o fato recebe um valor ou vários, com código e rótulo.</param>
/// <param name="Escopo">De quem é o fato, com código e rótulo.</param>
/// <param name="Formato">Formato do fato de texto, com código e rótulo.</param>
/// <param name="Valores">De onde vêm os valores do categórico e os que o catálogo declara.</param>
/// <param name="Resolucao">De onde o valor do fato vem e quando fica conhecido.</param>
/// <param name="Protecao">A proteção de dados do fato.</param>
public sealed record DefinicaoDoFatoDto(
    CodigoComRotulo<TipoDoDado> Dominio,
    CodigoComRotulo<QuantidadeDeValores> Cardinalidade,
    CodigoComRotulo<EscopoFato> Escopo,
    CodigoComRotulo<FormatoTexto>? Formato,
    ValoresDoFatoDto Valores,
    ResolucaoDoFatoDto Resolucao,
    ProtecaoDoFatoDto Protecao);

/// <summary>Os valores de um dado de opção.</summary>
/// <param name="Fonte">De onde vêm os valores, com código e rótulo; nula no dado que não é de opção.</param>
/// <param name="Declarados">Os valores que o catálogo declara, em ordem, ativos e desativados.</param>
public sealed record ValoresDoFatoDto(CodigoComRotulo<FonteValoresFato>? Fonte, IReadOnlyList<FatoValorDominioDto> Declarados);

/// <summary>De onde o valor do fato vem e quando fica conhecido.</summary>
/// <param name="Origem">Como o valor chega ao sistema, com código e rótulo: calculado, declarado ou de integração; não muda.</param>
/// <param name="PontoResolucao">Código canônico da fase em que o valor fica conhecido.</param>
/// <param name="Binding">Vínculo ao campo, ao atributo ou à regra que produz o valor; não muda.</param>
public sealed record ResolucaoDoFatoDto(CodigoComRotulo<OrigemFato> Origem, string PontoResolucao, string Binding);

/// <summary>A proteção de dados do fato e o circuito que a aprova.</summary>
/// <param name="Classificacao">Classificação de proteção, com código e rótulo.</param>
/// <param name="Finalidade">Para que o dado é tratado.</param>
/// <param name="HipoteseLegal">Hipótese legal de tratamento, com código e rótulo.</param>
/// <param name="Aprovacao">O circuito de aprovação da proteção de dados.</param>
public sealed record ProtecaoDoFatoDto(
    CodigoComRotulo<ClassificacaoDeProtecao> Classificacao,
    string Finalidade,
    CodigoComRotulo<HipoteseLegalTratamento> HipoteseLegal,
    AprovacaoDaProtecaoDto Aprovacao);

/// <summary>Um valor de domínio de um fato de fonte global.</summary>
public sealed record FatoValorDominioDto(string Codigo, string? Descricao, int Ordem, bool Ativo, string? Orientacao);

/// <summary>
/// Uma regra padrão do derivado: o que contribui (nulo no booleano) e o predicado, OU de cláusulas
/// que são E de condições; vazio na regra incondicional.
/// </summary>
public sealed record RegraPadraoDto(string? Contribui, IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoDto>> Quando);

