namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// O ato do processo foi registrado antes de o certame ter divulgação.
/// </summary>
/// <remarks>
/// É a corrida entre a divulgação do edital e a de um ato que não cria versão: o documento do ato só
/// entra numa divulgação que já existe, e a do edital pode ainda estar a caminho. Insistir resolve em
/// segundos ou poucos minutos; a mensagem que esgotar as tentativas vai para a fila morta, onde a
/// pendência fica visível e reprocessável.
/// </remarks>
public sealed class CertameAindaNaoDivulgadoException(string message) : Exception(message);
