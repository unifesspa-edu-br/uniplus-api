namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

/// <summary>
/// Os relatórios que o processo considera na importação do resultado (UNI-REQ-0151): os declarados no documento e
/// os que, mesmo retirados dele, já importaram algum candidato. Cada um com o leiaute copiado na versão que o
/// declarou, e nunca o do catálogo, que pode ter mudado ou saído de uso. Os relatórios com candidato importado
/// vêm da importação já feita; sem ela, a lista é vazia.
/// </summary>
public sealed record RelatoriosConsiderados(
    IReadOnlyList<LeiauteDoRelatorio> Declarados,
    IReadOnlyList<LeiauteDoRelatorio> ComCandidatoImportado)
{
    /// <summary>Nenhum relatório: o processo que não declara importação não traz dado de arquivo.</summary>
    public static RelatoriosConsiderados Nenhum { get; } = new([], []);

    /// <summary>Os declarados e os com candidato importado, cada cópia de leiaute uma só vez; a cópia de conteúdo diferente é outra.</summary>
    public IReadOnlyList<LeiauteDoRelatorio> Considerados =>
        [.. Declarados.Concat(ComCandidatoImportado).Distinct()];

    /// <summary>Se algum relatório considerado tem o fato entre os dados do arquivo, mesmo em coluna opcional, que pode vir preenchida.</summary>
    public bool Declara(string fato) => Considerados.Any(leiaute => leiaute.Declara(fato));

    /// <summary>Se algum relatório considerado traz o fato em toda linha do arquivo.</summary>
    public bool Traz(string fato) => Considerados.Any(leiaute => leiaute.Traz(fato));

    /// <summary>Se algum relatório considerado traz todos os fatos, juntos, em toda linha do arquivo.</summary>
    public bool TrazTodosEmAlgum(IReadOnlySet<string> fatos) =>
        Considerados.Any(leiaute => fatos.All(leiaute.Traz));

    /// <summary>Se algum relatório considerado tem todos os fatos entre os dados do arquivo, em coluna obrigatória ou opcional.</summary>
    public bool DeclaramTodosEmAlgum(IReadOnlySet<string> fatos) =>
        Considerados.Any(leiaute => fatos.All(leiaute.Declara));

    /// <summary>
    /// Os fatos do conjunto básico que o formulário obrigatório da primeira fase depois da importação pode coletar:
    /// os do dado que algum relatório considerado não traz inteiro, por falta da coluna, por coluna opcional ou por
    /// dado incompleto. O dado é o grupo de fatos da presença; o fato sem presença é um dado sozinho. Sem relatório
    /// considerado, o arquivo não traz dado algum.
    /// </summary>
    public IReadOnlySet<string> DadosBasicosAComplementar(
        IReadOnlySet<string> conjuntoBasico, IReadOnlyCollection<PresencaNaImportacao> presencas)
    {
        ArgumentNullException.ThrowIfNull(conjuntoBasico);
        ArgumentNullException.ThrowIfNull(presencas);

        IReadOnlyList<LeiauteDoRelatorio> relatorios = Considerados;
        IEnumerable<IReadOnlySet<string>> dados = presencas
            .Select(static presenca => presenca.FatosDoDadoBasico)
            .Concat(conjuntoBasico
                .Where(fato => !presencas.Any(presenca => presenca.FatosDoDadoBasico.Contains(fato)))
                .Select(static fato => (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal) { fato }));
        return dados
            .Where(dado => relatorios.Count == 0 || relatorios.Any(leiaute => !dado.All(leiaute.Traz)))
            .SelectMany(static dado => dado)
            .ToHashSet(StringComparer.Ordinal);
    }
}
