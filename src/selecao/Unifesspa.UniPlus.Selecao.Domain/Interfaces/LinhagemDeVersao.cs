namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Um degrau da linhagem de versões de um processo: o número da versão e o ato que a criou.
/// </summary>
/// <remarks>
/// Projeção estreita de propósito. Quem resolve a versão publicamente visível percorre a linhagem
/// do topo para baixo e só materializa a configuração congelada da versão eleita — carregar o
/// envelope de cada degrau para descartar quase todos seria pagar o documento inteiro por consulta.
/// </remarks>
/// <param name="NumeroVersao">Número da versão na cadeia, crescente.</param>
/// <param name="AtoCriadorId">Ato normativo que criou a versão, referenciado por valor.</param>
public readonly record struct LinhagemDeVersao(int NumeroVersao, Guid AtoCriadorId);
