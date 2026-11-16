namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cada fato tem um único campo que o produz no processo, em um único formulário (UNI-REQ-0144). A escolha
/// de fundamentos do pedido de isenção é registrada por formulário e fica fora da regra do campo produtor. O
/// dado básico que o formulário da primeira fase depois de uma importação também coleta não é conferido aqui,
/// porque a importação não é um formulário do processo. A recusa aponta o fato pelo caminho, uma vez por fato.
/// </summary>
public static class ConferenciaDoProdutorUnico
{
    public const string FatoDuplicado = "FatoColetado.FatoDuplicado";

    public static IReadOnlyList<FieldError> Conferir(IReadOnlyList<ColetaDoFormulario> coletas)
    {
        ArgumentNullException.ThrowIfNull(coletas);

        return [.. coletas
            .SelectMany(static coleta => coleta.FatosDosCampos)
            .GroupBy(static fato => fato, StringComparer.Ordinal)
            .Where(static produtores => produtores.Count() > 1)
            .Select(static produtores => new FieldError($"fatos.{produtores.Key}", new DomainError(
                FatoDuplicado, "O fato é coletado por mais de um campo no processo.")))];
    }
}
