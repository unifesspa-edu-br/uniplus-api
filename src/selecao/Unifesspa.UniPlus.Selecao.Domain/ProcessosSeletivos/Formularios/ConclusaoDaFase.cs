namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Quando o candidato conclui a fase pelos formulários (UNI-REQ-0211, UNI-REQ-0144): ao enviar todos os formulários
/// obrigatórios dela que se aplicam a ele, na versão dele. A fase em que nenhum obrigatório se aplica ao candidato
/// está cumprida por ele sem envio. Quem chama passa só os obrigatórios que se aplicam ao candidato; o rascunho não
/// conta como envio.
/// </summary>
public static class ConclusaoDaFase
{
    public static bool Cumprida(IEnumerable<CodigoDoFormulario> obrigatoriosQueSeAplicam, IReadOnlySet<CodigoDoFormulario> enviados)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriosQueSeAplicam);
        ArgumentNullException.ThrowIfNull(enviados);

        return obrigatoriosQueSeAplicam.All(enviados.Contains);
    }
}
