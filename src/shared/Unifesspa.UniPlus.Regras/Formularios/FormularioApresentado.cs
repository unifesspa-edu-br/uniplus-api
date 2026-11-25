namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// Um valor que o candidato pode escolher num campo de seleção: o código, a descrição — o nome da
/// opção —, a ordem e a orientação, o que o candidato precisa saber sobre a opção, mostrada abaixo dela.
/// </summary>
public sealed record ValorSelecionavel(string Codigo, string? Descricao, int Ordem, string? Orientacao = null);

/// <summary>
/// Um campo do formulário pronto para apresentação (UNI-REQ-0144): a apresentação e, no campo de
/// seleção, os valores que o candidato pode escolher — ao menos um —; nulos nos demais. O formato diz
/// como a resposta do campo de texto é conferida. As regras do campo estão nas regras do formulário,
/// pelo código do fato, e só lá (ADR-0139).
/// </summary>
public sealed record CampoApresentado(
    string FatoCodigo,
    int Ordem,
    string? EtapaCodigo,
    ApresentacaoDoCampoDto Apresentacao,
    string? Formato,
    IReadOnlyList<ValorSelecionavel>? ValoresSelecionaveis);

/// <summary>
/// Um grupo repetível pronto para apresentação (UNI-REQ-0146): a posição na ordem dos itens, a seção,
/// o rótulo, as ocorrências que admite — mínimo, máximo e se o próprio candidato é um dos membros — e os
/// campos que cada ocorrência responde. A exibição e a obrigatoriedade do grupo estão nas regras.
/// </summary>
public sealed record GrupoApresentado(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    OcorrenciasDoGrupoDto Ocorrencias,
    IReadOnlyList<CampoApresentado> Subitens);

/// <summary>
/// Um fato que as regras do formulário citam e ele não pergunta. Quando outro formulário o pergunta,
/// vem com a apresentação dele — rótulo, tipo de campo, formato e valores —, e a simulação o
/// pergunta como dado anterior. Quando nenhum formulário o pergunta, vem com o rótulo e o tipo do
/// catálogo; se é calculado, <see cref="CalculadoDe"/> diz de quais fatos: o sistema calcula a faixa
/// etária da data de nascimento, e o agregado sobre o grupo repetível de outro formulário, do fato de
/// membro que ele agrega.
/// </summary>
public sealed record FatoPressuposto(
    string FatoCodigo,
    string? Rotulo,
    [property: AceitaCodigosDe<TipoDeCampo>] string? TipoDeCampo,
    string? Formato,
    IReadOnlyList<ValorSelecionavel>? ValoresSelecionaveis,
    IReadOnlyList<string>? CalculadoDe);

/// <summary>
/// Uma seção ou um bloco do formulário pronto para apresentação. <see cref="CodigoNasRegras"/> é o
/// código com que a seção aparece nas regras — nulo no bloco, que não tem regra própria.
/// </summary>
public sealed record SecaoRenderizavel(
    string Codigo,
    string? CodigoNasRegras,
    int Ordem,
    [property: AceitaCodigosDe<TipoEtapaFormulario>] string Tipo,
    [property: AceitaCodigosDe<BlocoSistema>] string? Bloco,
    ApresentacaoDaEtapaDto Apresentacao);

/// <summary>
/// Um termo exigido pronto para apresentação (UNI-REQ-0086): a versão escolhida no catálogo, com o
/// conteúdo. <see cref="CodigoNasRegras"/> é o código com que o termo aparece nas regras, que dizem
/// quando ele aparece e quando é obrigatório.
/// </summary>
public sealed record TermoApresentado(
    string Codigo,
    string CodigoNasRegras,
    int Ordem,
    Guid TermoId,
    Guid VersaoId,
    ConteudoDoTermoDto Conteudo);

/// <summary>Os campos e os grupos que o formulário pergunta ao candidato, na ordem de apresentação.</summary>
public sealed record ColetaApresentada(
    IReadOnlyList<CampoApresentado> Itens,
    IReadOnlyList<GrupoApresentado> Grupos);

/// <summary>
/// O que o interpretador recebe como já conhecido: os fatos que as regras citam e o formulário não
/// pergunta, a data em que os fatos que dependem do tempo, como a faixa etária, são calculados — nula
/// quando o processo não declara referência temporal — e a zona, no identificador IANA, em que se conta
/// o dia do envio, de que partem os limites da faixa de data: a do período de inscrição, nula no modelo,
/// que não tem período de inscrição.
/// </summary>
public sealed record PressupostosDoFormulario(
    IReadOnlyList<FatoPressuposto> Fatos,
    DateOnly? DataReferenciaFatos,
    string? FusoDoEnvio);

/// <summary>
/// O formulário de uma finalidade pronto para o interpretador do front (UNI-REQ-0144, ADR-0139): a
/// apresentação — título, seções, termos e a coleta de campos e grupos —, as regras que o front interpreta
/// e os pressupostos. É o mesmo formato para o certame divulgado, o rascunho do processo e o modelo, e o
/// que a simulação importa e exporta.
/// </summary>
/// <param name="Regras">
/// A exibição e a obrigatoriedade de cada seção, campo, grupo e termo, as restrições, os impedimentos
/// e a oferta de valores, com as derivações e os agregados que o formulário cita. A API confere de
/// novo tudo o que recebe.
/// </param>
/// <param name="Pressupostos">
/// Os fatos que as regras citam e o formulário não pergunta, com a data de referência e o fuso do envio.
/// </param>
public sealed record FormularioApresentado(
    string Finalidade,
    string? Titulo,
    IReadOnlyList<SecaoRenderizavel> Etapas,
    IReadOnlyList<TermoApresentado> Termos,
    ColetaApresentada Coleta,
    FormularioPortavel Regras,
    PressupostosDoFormulario Pressupostos);
