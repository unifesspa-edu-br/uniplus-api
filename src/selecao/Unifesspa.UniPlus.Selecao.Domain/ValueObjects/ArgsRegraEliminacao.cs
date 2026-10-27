namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json.Serialization;

/// <summary>
/// Args aplicados de uma <see cref="Entities.RegraEliminacao"/> — os
/// parâmetros que o admin preenche ao aplicar uma regra do
/// <c>rol_de_regras</c> (<c>tipo=regra_eliminacao</c>), tipados por variante
/// conforme o <c>esquema_args</c> de cada código.
/// Discriminated union no molde de <c>PredicadoObrigatoriedade</c> (ADR-0058)
/// e de <c>ArgsCriterioDesempate</c> (Story #774).
/// </summary>
/// <remarks>
/// Forma fechada por design: as variantes espelham as regras de eliminação semeadas em
/// <c>rol_de_regras</c> — <c>ELIM-NOTA-MINIMA-ETAPA</c>, <c>ELIM-CORTE-EM-AREA</c>,
/// <c>ELIM-ZERO-EM-AREA</c>, <c>ELIM-FALTA-EM-DIA-DE-PROVA-ENEM</c> e
/// <c>ELIM-AUSENCIA-NA-ETAPA</c>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(ArgsElimNotaMinimaEtapa), "notaMinimaEtapa")]
[JsonDerivedType(typeof(ArgsElimCorteEmArea), "corteEmArea")]
[JsonDerivedType(typeof(ArgsElimZeroEmArea), "zeroEmArea")]
[JsonDerivedType(typeof(ArgsElimFaltaEmDiaDeProvaEnem), "faltaEmDiaDeProvaEnem")]
[JsonDerivedType(typeof(ArgsElimAusenciaNaEtapa), "ausenciaNaEtapa")]
public abstract record ArgsRegraEliminacao
{
    /// <summary>
    /// A regra só se aplica a classificação baseada em ENEM, porque só tem sentido sobre os
    /// dados do ENEM do candidato (notas por área, presença em cada dia de prova). É método, e
    /// não propriedade, para ficar fora do JSON persistido e do envelope canônico: é
    /// comportamento do tipo, não dado declarado.
    /// </summary>
    public abstract bool ExigeEnem();

    /// <summary>
    /// A etapa do processo que a regra cita, quando cita: é dela que dependem a remoção da
    /// etapa, a existência no grafo e a conferência da etapa citada. Método pelo mesmo motivo
    /// de <see cref="ExigeEnem"/>.
    /// </summary>
    public virtual Guid? EtapaReferenciada() => null;
}

/// <summary>Elimina quando a nota da etapa referenciada é menor que <see cref="NotaMinima"/>. A etapa deve existir no mesmo processo (INV-B4).</summary>
public sealed record ArgsElimNotaMinimaEtapa(Guid EtapaRef, decimal NotaMinima) : ArgsRegraEliminacao
{
    public override bool ExigeEnem() => false;

    public override Guid? EtapaReferenciada() => EtapaRef;
}

/// <summary>
/// Elimina quem faltou à etapa referenciada, tenha ela nota ou não. A etapa deve existir no
/// mesmo processo e não pode ser a do ENEM, cuja ausência é a
/// <see cref="ArgsElimFaltaEmDiaDeProvaEnem"/>.
/// </summary>
public sealed record ArgsElimAusenciaNaEtapa(Guid EtapaRef) : ArgsRegraEliminacao
{
    public override bool ExigeEnem() => false;

    public override Guid? EtapaReferenciada() => EtapaRef;
}

/// <summary>
/// Elimina quando a nota do candidato na área do ENEM <see cref="AreaCodigo"/> é menor que
/// <see cref="Minimo"/> (ex.: Redação 400 — Res. 805 Anexo I). A área é citada pelo código do
/// quadro de pesos por área.
/// </summary>
public sealed record ArgsElimCorteEmArea(string AreaCodigo, decimal Minimo) : ArgsRegraEliminacao
{
    public override bool ExigeEnem() => true;
}

/// <summary>Elimina quando há nota zero em qualquer área do ENEM (Res. 805 art. 5º). Sem args.</summary>
public sealed record ArgsElimZeroEmArea : ArgsRegraEliminacao
{
    public override bool ExigeEnem() => true;
}

/// <summary>
/// Elimina quem faltou a pelo menos um dia de prova da edição do ENEM usada no processo. Sem
/// args. Ficar sem nota por falta é diferente de tirar zero, que é <see cref="ArgsElimZeroEmArea"/>:
/// quem tira zero compareceu.
/// </summary>
public sealed record ArgsElimFaltaEmDiaDeProvaEnem : ArgsRegraEliminacao
{
    public override bool ExigeEnem() => true;
}
