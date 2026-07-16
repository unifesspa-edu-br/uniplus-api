namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Quem controla a data de uma <see cref="Entities.FaseCronograma"/> — snapshot-copy
/// (ADR-0061) do <c>OrigemData</c> vigente na <c>FaseCanonica</c> de origem (módulo
/// Configuração) no momento em que a fase entrou no cronograma.
/// </summary>
/// <remarks>
/// Enum <b>próprio</b> do Domain de Seleção — não é referência cross-módulo ao enum
/// homônimo de Configuração (ADR-0042: Domain não depende de outro módulo). Mesmos
/// tokens/padrão, dois tipos distintos.
/// <para>
/// A janela obrigatória/opcional da fase decorre <b>apenas</b> deste atributo (CA-07):
/// <see cref="Propria"/> exige <c>Inicio</c>/<c>Fim</c>; <see cref="Delegada"/> aceita
/// "sem data" como estado válido.
/// </para>
/// </remarks>
public enum OrigemDataFase
{
    Nenhuma = 0,

    /// <summary>O setor responsável pelo processo controla e congela a data — janela obrigatória.</summary>
    Propria = 1,

    /// <summary>Um agente externo (MEC, calendário acadêmico) controla a data — janela opcional.</summary>
    Delegada = 2,
}
