namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Services;

/// <summary>Um valor que o candidato pode escolher num campo de seleção: o código, a descrição que orienta a escolha e a ordem.</summary>
public sealed record ValorSelecionavel(string Codigo, string? Descricao, int Ordem);

/// <summary>
/// Um campo do formulário pronto para renderização (UNI-REQ-0144): a apresentação e, no campo de
/// seleção, os valores que o candidato pode escolher — ao menos um —; nulos nos demais. O formato diz
/// como a resposta do campo de texto é conferida. As regras do campo estão nas regras do formulário,
/// pelo código do fato, e só lá (ADR-0139).
/// </summary>
public sealed record CampoRenderizavel(
    string FatoCodigo,
    int Ordem,
    string Rotulo,
    string TipoRenderizacao,
    IReadOnlyList<ValorSelecionavel>? ValoresSelecionaveis,
    string? EtapaCodigo,
    string? Formato,
    string? Ajuda,
    bool PedirConfirmacao);

/// <summary>
/// Um grupo repetível pronto para renderização (UNI-REQ-0146): a posição na ordem dos itens, a seção,
/// o rótulo, o mínimo e o máximo de ocorrências, se o próprio candidato é um dos membros e os campos
/// que cada ocorrência responde. A exibição e a obrigatoriedade do grupo estão nas regras.
/// </summary>
public sealed record GrupoRenderizavel(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    int Minimo,
    int? Maximo,
    bool IncluiCandidato,
    IReadOnlyList<CampoRenderizavel> Subitens);

/// <summary>
/// Um fato que as regras do formulário citam e ele não pergunta. Quando outro formulário o pergunta,
/// vem com a apresentação dele — rótulo, tipo de renderização, formato e valores —, e a simulação o
/// pergunta como dado anterior. Quando é calculado, <see cref="CalculadoDe"/> diz de quais fatos: o
/// sistema calcula a faixa etária da data de nascimento, e o agregado sobre o grupo repetível de
/// outro formulário, do fato de membro que ele agrega. O que a classificação produz, como o grupo de
/// vagas em que o candidato foi convocado, vem só com o código.
/// </summary>
public sealed record PressupostoRenderizavel(
    string FatoCodigo,
    string? Rotulo,
    string? TipoRenderizacao,
    string? Formato,
    IReadOnlyList<ValorSelecionavel>? ValoresSelecionaveis,
    IReadOnlyList<string>? CalculadoDe);

/// <summary>
/// Uma seção ou um bloco do formulário pronto para renderização. <see cref="CodigoNasRegras"/> é o
/// código com que a seção aparece nas regras — nulo no bloco, que não tem regra própria.
/// </summary>
public sealed record SecaoRenderizavel(
    string Codigo,
    string? CodigoNasRegras,
    int Ordem,
    string Tipo,
    string? Bloco,
    string Titulo,
    string? Descricao,
    string? Aviso);

/// <summary>
/// Um termo exigido pronto para renderização (UNI-REQ-0086): a versão escolhida no catálogo, com o
/// conteúdo. <see cref="CodigoNasRegras"/> é o código com que o termo aparece nas regras, que dizem
/// quando ele aparece e quando é obrigatório.
/// </summary>
public sealed record TermoRenderizavel(
    string Codigo,
    string CodigoNasRegras,
    int Ordem,
    Guid TermoId,
    Guid VersaoId,
    string Nome,
    string Texto,
    string BaseLegal,
    string FormaAceite,
    string HashVersao);

/// <summary>
/// O formulário de uma finalidade pronto para o interpretador do front (UNI-REQ-0144, ADR-0139): a
/// apresentação — título, seções, termos, campos e grupos —, as regras que o front interpreta e os
/// pressupostos. É o mesmo formato para o certame divulgado, o rascunho do processo e o modelo, e o
/// que a simulação importa e exporta. Aberto para o certame acrescentar o que só ele tem, como a
/// comprovação documental.
/// </summary>
/// <param name="Regras">
/// A exibição e a obrigatoriedade de cada seção, campo, grupo e termo, as restrições, os impedimentos
/// e a oferta de valores, com as derivações e os agregados que o formulário cita. A API confere de
/// novo tudo o que recebe.
/// </param>
/// <param name="Pressupostos">
/// Os fatos que as regras citam e o formulário não pergunta, que o interpretador recebe como já
/// conhecidos.
/// </param>
/// <param name="DataReferenciaFatos">
/// A data em que os fatos que dependem do tempo, como a faixa etária, são calculados. Nula quando o
/// processo não declara referência temporal.
/// </param>
public record FormularioRenderizavel(
    string Finalidade,
    string? Titulo,
    IReadOnlyList<SecaoRenderizavel> Etapas,
    IReadOnlyList<TermoRenderizavel> Termos,
    IReadOnlyList<CampoRenderizavel> FatosColetados,
    IReadOnlyList<GrupoRenderizavel> Grupos,
    FormularioPortavel Regras,
    IReadOnlyList<PressupostoRenderizavel> Pressupostos,
    DateOnly? DataReferenciaFatos)
{
    /// <summary>
    /// Monta o formulário a partir da apresentação, do recorte das regras da finalidade e do que
    /// descreve os pressupostos: os campos dos outros formulários, que dão a apresentação do que eles
    /// perguntam, e os agregados sobre os grupos, que com os derivados do sistema dizem de que fatos
    /// cada fato calculado é calculado.
    /// </summary>
    public static FormularioRenderizavel Montar(
        string finalidade,
        string? titulo,
        IReadOnlyList<SecaoRenderizavel> etapas,
        IReadOnlyList<TermoRenderizavel> termos,
        IReadOnlyList<CampoRenderizavel> campos,
        IReadOnlyList<GrupoRenderizavel> grupos,
        RecorteDaFinalidade recorte,
        IEnumerable<CampoRenderizavel> camposDosOutrosFormularios,
        IEnumerable<DefinicaoAgregado> agregados,
        DateOnly? dataReferenciaFatos)
    {
        ArgumentNullException.ThrowIfNull(recorte);
        ArgumentNullException.ThrowIfNull(camposDosOutrosFormularios);
        ArgumentNullException.ThrowIfNull(agregados);

        Dictionary<string, IReadOnlyList<string>> calculadoDe = new(DerivadosDoSistema.Dependencias, StringComparer.Ordinal);
        foreach (DefinicaoAgregado agregado in agregados)
        {
            calculadoDe[agregado.Codigo] = [agregado.FatoDeMembro];
        }

        Dictionary<string, CampoRenderizavel> perguntados = new(StringComparer.Ordinal);
        foreach (CampoRenderizavel campo in camposDosOutrosFormularios)
        {
            perguntados.TryAdd(campo.FatoCodigo, campo);
        }

        IReadOnlyList<PressupostoRenderizavel> pressupostos = [.. recorte.Pressupostos.Select(codigo =>
            perguntados.TryGetValue(codigo, out CampoRenderizavel? campo)
                ? new PressupostoRenderizavel(codigo, campo.Rotulo, campo.TipoRenderizacao, campo.Formato, campo.ValoresSelecionaveis, null)
                : new PressupostoRenderizavel(codigo, null, null, null, null, calculadoDe.GetValueOrDefault(codigo)))];

        return new FormularioRenderizavel(
            finalidade, titulo, etapas, termos, campos, grupos, recorte.Regras, pressupostos, dataReferenciaFatos);
    }
}
