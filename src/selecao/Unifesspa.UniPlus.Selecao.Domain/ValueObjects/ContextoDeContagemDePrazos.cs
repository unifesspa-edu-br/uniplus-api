namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que a raiz precisa saber, e não consegue saber sozinha, para decidir sobre as contagens de
/// prazo do certame (UNI-REQ-0116). Chega pronto da Application: o Domain não lê reader nem
/// serviço.
/// </summary>
/// <param name="CalendarioVigente">
/// O calendário de dias úteis vigente no momento da operação, já copiado por valor, ou
/// <see langword="null"/> quando não há dataset marcado vigente. É a mesma leitura que alimenta
/// o gate e o congelamento — nunca uma para validar e outra para congelar, senão a versão
/// publicada poderia carregar um dataset que o gate não aprovou.
/// </param>
/// <param name="FusoInstitucionalReconhecido">
/// Se a base de fusos do runtime reconhece a zona institucional. Vem resolvido de fora porque o
/// agregado não tem como distinguir "fuso irresolvível" de "fuso ausente" — ele não declara
/// fuso, o sistema o aplica.
/// </param>
/// <param name="FalhaDoCalendarioVigente">
/// A recusa, quando existe dataset vigente mas ele não pôde ser convertido em snapshot — dado
/// incoerente no cadastro de origem. Distinta de <see cref="CalendarioVigente"/> nulo, que é
/// ausência: só quem <b>precisa</b> do calendário é barrado por ela, e um processo sem contagem
/// sobre dia útil publica normalmente, porque não usa o dado que está quebrado.
/// <para>
/// O estado viaja separado em vez de virar falha imediata no handler. Convertê-lo em ausência
/// deixaria o checklist verde para um processo que a publicação recusa; abortar no handler
/// recusaria também quem não depende do calendário. Guardá-lo aqui mantém a decisão num lugar
/// só — o gate da raiz —, e é o que preserva a bicondicionalidade entre o preflight e a recusa.
/// </para>
/// </param>
/// <remarks>
/// <para>
/// <b>Por que o fuso não vira gate da raiz.</b> Zona irresolvível é defeito de instalação, não
/// da configuração: nenhum campo do processo o produz, e o contrato de erro o mapeia para 500,
/// junto com as demais causas internas. Quem publica não tem o que corrigir. Ele entra aqui
/// para ser <b>projetado</b> no checklist — quem consulta o preflight precisa saber que o
/// ambiente está quebrado antes de tentar publicar e receber um 500 sem explicação —, e a
/// recusa continua sendo do handler, onde a causa é reconhecível.
/// </para>
/// </remarks>
public sealed record ContextoDeContagemDePrazos(
    CalendarioDiasUteisCongelado? CalendarioVigente,
    bool FusoInstitucionalReconhecido,
    DomainError? FalhaDoCalendarioVigente = null)
{
    /// <summary>
    /// Contexto de um ambiente sem calendário vigente e com fuso reconhecido — o estado de um
    /// sistema recém-instalado, e o default de teste que não exercita nenhuma das duas causas.
    /// </summary>
    public static ContextoDeContagemDePrazos SemCalendario { get; } = new(null, true);
}
