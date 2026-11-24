namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O mecanismo que produz o valor de um dado do catálogo, que o vínculo do dado grava antes dos dois pontos
/// (<c>MECANISMO:REFERENCIA</c>; ADR-0116, ADR-0136): o prefixo nomeia o mecanismo, e a referência, o campo, a regra
/// ou o atributo. Um conjunto só, lido pela Configuração, que grava o vínculo, e pela Seleção, que decide por ele
/// (ADR-0176).
/// </summary>
[DescricaoDoContrato("O mecanismo que produz o valor de um dado do catálogo: campo do formulário, atributo do candidato, regra, classificação, integração ou resumo de grupo.")]
public enum MecanismoDoVinculo
{
    /// <summary>Respondido pelo candidato num campo de formulário — o único vínculo coletável.</summary>
    [CodigoDeContrato("CAMPO_FORMULARIO", "Campo do formulário", Descricao = "O candidato responde o valor num campo do formulário; é o único vínculo que o formulário coleta.")]
    CampoFormulario = 1,

    /// <summary>Calculado pelo sistema de atributos do candidato, como a faixa etária.</summary>
    [CodigoDeContrato("ATRIBUTO_CANDIDATO", "Atributo do candidato", Descricao = "O valor é calculado pelo sistema a partir de um atributo do candidato, como a faixa etária.")]
    AtributoCandidato = 2,

    /// <summary>Calculado pela regra declarada no processo, como a modalidade de concorrência.</summary>
    [CodigoDeContrato("REGRA_DERIVACAO", "Regra de derivação", Descricao = "O valor é calculado por uma regra declarada no processo, como a modalidade de concorrência.")]
    RegraDeDerivacao = 3,

    /// <summary>Produzido pela classificação, como o grupo em que o candidato foi convocado.</summary>
    [CodigoDeContrato("CLASSIFICACAO", "Classificação", Descricao = "O valor é produzido pela classificação, como o grupo em que o candidato foi convocado.")]
    Classificacao = 4,

    /// <summary>Recebido de integração externa.</summary>
    [CodigoDeContrato("INTEGRACAO", "Integração", Descricao = "O valor é recebido de uma integração com outro sistema.")]
    Integracao = 5,

    /// <summary>
    /// Resumo sobre um grupo repetível (ADR-0138), pelo dado de membro que a referência nomeia — o dado de membro tem um
    /// só produtor no processo, então identifica o grupo.
    /// </summary>
    [CodigoDeContrato("RESUMO_DE_GRUPO", "Resumo de grupo", Descricao = "O valor resume um grupo repetível do formulário, pelo dado de membro que a referência nomeia.")]
    ResumoDeGrupo = 6,
}
