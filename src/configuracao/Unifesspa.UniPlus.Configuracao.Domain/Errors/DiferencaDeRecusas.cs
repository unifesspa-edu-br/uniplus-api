namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Separa a recusa que uma alteração causa da que já existia antes dela. A identidade da recusa é o
/// campo, o código e a origem (a regra e o fato citado que a causam, quando há); a mensagem não conta, porque embute valores (ponto de resolução, código de
/// fato) que a própria alteração muda sem que o motivo mude.
/// </summary>
public static class DiferencaDeRecusas
{
    public static List<FieldError> Novas(IEnumerable<FieldError> depois, IEnumerable<FieldError> antes)
    {
        ArgumentNullException.ThrowIfNull(depois);
        ArgumentNullException.ThrowIfNull(antes);

        HashSet<(string?, string, string?)> existentes = [.. antes.Select(Chave)];
        return [.. depois.Where(e => !existentes.Contains(Chave(e)))];
    }

    private static (string?, string, string?) Chave(FieldError e) => (e.Field, e.Error.Code, e.Origem);
}
