namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o processo de resultado importado declara da importação (UNI-REQ-0144): os relatórios que importa, cada
/// um pelo leiaute, e a primeira fase depois da importação, em que o formulário obrigatório coleta o dado básico
/// que o arquivo não traz. A declaração é do próprio processo, nunca deduzida do tipo de processo. A declaração
/// que não existe ainda é a do processo por declarar, e quem a guarda a representa por ausência.
/// </summary>
public sealed record DeclaracaoDaImportacaoDoResultado
{
    private const string CampoDosRelatorios = "importacao.relatorios";

    private DeclaracaoDaImportacaoDoResultado(IReadOnlyList<RelatorioDeclarado> relatorios, Guid? primeiraFaseDepoisDaImportacaoId)
    {
        Relatorios = relatorios;
        PrimeiraFaseDepoisDaImportacaoId = primeiraFaseDepoisDaImportacaoId;
    }

    /// <summary>Os relatórios declarados; vazia no rascunho que ainda não os declarou.</summary>
    public IReadOnlyList<RelatorioDeclarado> Relatorios { get; }

    /// <summary>A primeira fase depois da importação; nula enquanto o rascunho não a declara.</summary>
    public Guid? PrimeiraFaseDepoisDaImportacaoId { get; }

    /// <summary>As declarações são iguais quando têm os mesmos relatórios, na mesma ordem, e a mesma primeira fase.</summary>
    public bool Equals(DeclaracaoDaImportacaoDoResultado? outra) =>
        outra is not null
        && PrimeiraFaseDepoisDaImportacaoId == outra.PrimeiraFaseDepoisDaImportacaoId
        && Relatorios.SequenceEqual(outra.Relatorios);

    public override int GetHashCode() => HashCode.Combine(PrimeiraFaseDepoisDaImportacaoId, Relatorios.Count);

    /// <summary>
    /// Confere a forma da declaração e acumula as recusas (ADR-0125): cada relatório informa o código e a versão
    /// do leiaute, e nenhum leiaute se repete. A declaração sem relatório ainda é uma declaração: a publicação
    /// é que a recusa. A existência do leiaute e a primeira fase são conferidas pela conferência da declaração.
    /// </summary>
    public static Result<DeclaracaoDaImportacaoDoResultado> Declarar(
        IReadOnlyList<RelatorioInformado> relatorios, Guid? primeiraFaseDepoisDaImportacaoId)
    {
        ArgumentNullException.ThrowIfNull(relatorios);

        List<FieldError> recusas = [.. RecusasDeCadaRelatorio(relatorios)];
        return recusas.Count > 0
            ? Result<DeclaracaoDaImportacaoDoResultado>.ValidationFailure(recusas)
            : Result<DeclaracaoDaImportacaoDoResultado>.Success(new(
                [.. relatorios.Select(static relatorio => new RelatorioDeclarado(relatorio.LeiauteCodigo!, relatorio.LeiauteVersao!))],
                primeiraFaseDepoisDaImportacaoId));
    }

    private static IEnumerable<FieldError> RecusasDeCadaRelatorio(IReadOnlyList<RelatorioInformado> relatorios)
    {
        HashSet<string> jaDeclarados = new(StringComparer.Ordinal);
        for (int indice = 0; indice < relatorios.Count; indice++)
        {
            string campo = $"{CampoDosRelatorios}[{indice}]";
            foreach (FieldError recusa in RecusasDoLeiaute(relatorios[indice], campo).Concat(RecusaDoRepetido(relatorios[indice], jaDeclarados, campo)))
            {
                yield return recusa;
            }
        }
    }

    private static IEnumerable<FieldError> RecusaDoRepetido(RelatorioInformado? relatorio, HashSet<string> jaDeclarados, string campo)
    {
        if (!string.IsNullOrWhiteSpace(relatorio?.LeiauteCodigo) && !jaDeclarados.Add(relatorio.LeiauteCodigo))
        {
            yield return Recusa($"{campo}.leiauteCodigo", ImportacaoDoResultadoErrorCodes.RelatorioRepetido, "O leiaute já está declarado como relatório do processo.");
        }
    }

    private static IEnumerable<FieldError> RecusasDoLeiaute(RelatorioInformado? relatorio, string campo)
    {
        if (string.IsNullOrWhiteSpace(relatorio?.LeiauteCodigo))
        {
            yield return Recusa($"{campo}.leiauteCodigo", ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute, "O relatório declara o código do leiaute.");
        }

        if (string.IsNullOrWhiteSpace(relatorio?.LeiauteVersao))
        {
            yield return Recusa($"{campo}.leiauteVersao", ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute, "O relatório declara a versão do leiaute.");
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
