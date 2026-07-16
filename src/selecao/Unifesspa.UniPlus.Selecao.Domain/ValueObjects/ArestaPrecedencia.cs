namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma aresta do grafo de precedência entre fases canônicas (Story #851 §3.3) — cópia,
/// dentro do Domain de Seleção, do que <c>IPrecedenciaFaseReader.ListarVivasAsync</c>
/// (módulo Configuração, ADR-0056) devolve.
/// </summary>
/// <remarks>
/// O grafo é <b>parâmetro</b>, não navegação: o domínio nunca injeta o reader
/// (ADR-0042) — quem chama (o handler da Application) resolve o grafo vigente e o passa
/// já pronto a <see cref="Entities.ProcessoSeletivo.DefinirCronogramaFases"/>. Tipo
/// próprio (não referência a <c>Unifesspa.UniPlus.Configuracao.Contracts.PrecedenciaFaseView</c>)
/// pela mesma razão: Domain não depende do contrato de outro módulo.
/// </remarks>
/// <param name="AntecessoraCodigo">Código canônico da fase que precede.</param>
/// <param name="SucessoraCodigo">Código canônico da fase que sucede.</param>
/// <param name="PermiteSobreposicao">Se as janelas das duas fases podem se sobrepor quando ambas estão no cronograma.</param>
public sealed record ArestaPrecedencia(
    string AntecessoraCodigo,
    string SucessoraCodigo,
    bool PermiteSobreposicao);
