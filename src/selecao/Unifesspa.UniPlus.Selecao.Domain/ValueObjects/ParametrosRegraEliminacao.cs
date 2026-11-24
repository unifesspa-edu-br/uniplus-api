namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Parâmetros aplicados de uma <see cref="Entities.RegraEliminacao"/> — os
/// parâmetros que quem configura o processo preenche ao aplicar uma regra do
/// <c>regras_do_catalogo</c> (<c>tipo=regra_eliminacao</c>), tipados por variante
/// conforme o esquema do documento da configuração.
/// Conjunto fechado de variantes no molde de <c>PredicadoObrigatoriedade</c> (ADR-0058)
/// e de <c>ParametrosCriterioDesempate</c>.
/// </summary>
/// <remarks>
/// Forma fechada por design: as variantes espelham as regras de eliminação semeadas em
/// <c>regras_do_catalogo</c> — <c>ELIMINACAO_NOTA_MINIMA_ETAPA</c>, <c>ELIMINACAO_CORTE_EM_AREA</c>,
/// <c>ELIMINACAO_ZERO_EM_AREA</c>, <c>ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM</c> e
/// <c>ELIMINACAO_AUSENCIA_NA_ETAPA</c>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(ParametrosEliminacaoPorNotaMinimaNaEtapa), "notaMinimaEtapa")]
[JsonDerivedType(typeof(ParametrosEliminacaoPorCorteEmAreaDoEnem), "corteEmArea")]
[JsonDerivedType(typeof(ParametrosEliminacaoPorZeroEmAreaDoEnem), "zeroEmArea")]
[JsonDerivedType(typeof(ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem), "faltaEmDiaDeProvaEnem")]
[JsonDerivedType(typeof(ParametrosEliminacaoPorAusenciaNaEtapa), "ausenciaNaEtapa")]
public abstract record ParametrosRegraEliminacao
{
    /// <summary>
    /// A regra só se aplica a classificação baseada em ENEM, porque só tem sentido sobre os
    /// dados do ENEM do candidato (notas por área, presença em cada dia de prova). É método, e
    /// não propriedade, para ficar fora do JSON persistido e do envelope canônico: é
    /// comportamento do tipo, não dado declarado.
    /// </summary>
    public abstract RegraDeEliminacaoPorCalculo RegraCorrespondente();

    internal virtual DomainError? RecusaDosValores() => null;

    public abstract bool ExigeEnem();

    /// <summary>
    /// A etapa do processo que a regra cita, quando cita: é dela que dependem a remoção da
    /// etapa, a existência no grafo e a conferência da etapa citada. Método pelo mesmo motivo
    /// de <see cref="ExigeEnem"/>.
    /// </summary>
    public virtual Guid? EtapaReferenciada() => null;
}

/// <summary>Elimina quando a nota da etapa referenciada é menor que <see cref="NotaMinima"/>. A etapa deve existir no mesmo processo.</summary>
public sealed record ParametrosEliminacaoPorNotaMinimaNaEtapa(Guid EtapaRef, decimal NotaMinima) : ParametrosRegraEliminacao
{
    public override RegraDeEliminacaoPorCalculo RegraCorrespondente() => RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa;

    internal override DomainError? RecusaDosValores() => NotaMinima < 0
        ? new DomainError("RegraEliminacao.NotaMinimaInvalida", "A nota mínima da eliminação deve ser não negativa.")
        : null;

    public override bool ExigeEnem() => false;

    public override Guid? EtapaReferenciada() => EtapaRef;
}

/// <summary>
/// Elimina quem faltou à etapa referenciada, tenha ela nota ou não. A etapa deve existir no
/// mesmo processo e não pode ser a do ENEM, cuja ausência é a
/// <see cref="ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem"/>.
/// </summary>
public sealed record ParametrosEliminacaoPorAusenciaNaEtapa(Guid EtapaRef) : ParametrosRegraEliminacao
{
    public override RegraDeEliminacaoPorCalculo RegraCorrespondente() => RegraDeEliminacaoPorCalculo.AusenciaNaEtapa;

    public override bool ExigeEnem() => false;

    public override Guid? EtapaReferenciada() => EtapaRef;
}

/// <summary>
/// Elimina quando a nota do candidato na área do ENEM <see cref="AreaCodigo"/> é menor que
/// <see cref="Minimo"/> (ex.: Redação 400 — Res. 805 Anexo I). A área é citada pelo código do
/// quadro de pesos por área.
/// </summary>
public sealed record ParametrosEliminacaoPorCorteEmAreaDoEnem(string AreaCodigo, decimal Minimo) : ParametrosRegraEliminacao
{
    public override RegraDeEliminacaoPorCalculo RegraCorrespondente() => RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem;

    internal override DomainError? RecusaDosValores() => !FormaDoCodigoDeAreaCitada.Aceita(AreaCodigo)
        ? new DomainError("RegraEliminacao.AreaInvalida", $"A área do corte é {FormaDoCodigoDeAreaCitada.Descricao}.")
        : Minimo < 0
            ? new DomainError("RegraEliminacao.MinimoInvalido", "O mínimo do corte em área deve ser não negativo.")
            : null;

    public override bool ExigeEnem() => true;
}

/// <summary>Elimina quando há nota zero em qualquer área do ENEM (Res. 805 art. 5º). Sem parâmetros.</summary>
public sealed record ParametrosEliminacaoPorZeroEmAreaDoEnem : ParametrosRegraEliminacao
{
    public override RegraDeEliminacaoPorCalculo RegraCorrespondente() => RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem;

    public override bool ExigeEnem() => true;
}

/// <summary>
/// Elimina quem faltou a pelo menos um dia de prova da edição do ENEM usada no processo. Sem
/// parâmetros. Ficar sem nota por falta é diferente de tirar zero, que é <see cref="ParametrosEliminacaoPorZeroEmAreaDoEnem"/>:
/// quem tira zero compareceu.
/// </summary>
public sealed record ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem : ParametrosRegraEliminacao
{
    public override RegraDeEliminacaoPorCalculo RegraCorrespondente() => RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem;

    public override bool ExigeEnem() => true;
}
