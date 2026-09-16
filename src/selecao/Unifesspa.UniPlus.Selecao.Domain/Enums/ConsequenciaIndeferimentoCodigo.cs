namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using System.Collections.Immutable;

/// <summary>
/// O que acontece com quem não entrega o documento, ou tem a entrega indeferida — vocabulário
/// fechado do wire de comando, em UPPER_SNAKE.
/// </summary>
/// <remarks>
/// <para>
/// É <see langword="string"/>, e não enum, porque a consequência não tem comportamento próprio
/// no domínio: ela é comparada contra a ação declarada pela modalidade da vaga e projetada
/// para o wire, nunca ramificada. O que faltava era uma fonte só — a lista estava escrita
/// palavra por palavra na exigência, no nó da árvore e no validator, e as três precisavam ser
/// lembradas juntas a cada valor novo.
/// </para>
/// <para>
/// Não confundir com <c>AcaoQuandoIndeferido</c>, do módulo Configuração: aquele é o
/// vocabulário da VAGA, e a diferença de grafia entre os dois (<see cref="ReclassificaAc"/>
/// contra <c>RECLASSIFICAR_AC</c>) é justamente o que registra que são dois vocabulários, com
/// uma ponte explícita entre eles na conferência de coerência.
/// </para>
/// </remarks>
public static class ConsequenciaIndeferimentoCodigo
{
    /// <summary>O candidato sai do processo.</summary>
    public const string Elimina = "ELIMINA";

    /// <summary>O candidato perde a vaga reservada e volta a concorrer na ampla concorrência.</summary>
    public const string ReclassificaAc = "RECLASSIFICA_AC";

    /// <summary>O candidato perde a vantagem que pleiteava, sem sair do processo.</summary>
    public const string RemoveVantagem = "REMOVE_VANTAGEM";

    /// <summary>
    /// Abre prazo para reenviar o documento. Só é aceita em fase que admite complementação.
    /// </summary>
    public const string PendenciaReenvio = "PENDENCIA_REENVIO";

    /// <summary>As quatro consequências que o wire aceita.</summary>
    public static readonly ImmutableArray<string> Validas =
        [Elimina, ReclassificaAc, RemoveVantagem, PendenciaReenvio];

    /// <summary>Se o código informado pertence ao vocabulário.</summary>
    public static bool EhValida(string? codigo) =>
        codigo is not null && Validas.Contains(codigo, StringComparer.Ordinal);
}
