namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

using CampoDoMembro = (string FatoCodigo, bool TemExibicao, Unifesspa.UniPlus.Regras.Formularios.Obrigatoriedade Obrigatoriedade, System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Formularios.RestricaoValor> Restricoes);

/// <summary>
/// As ocorrências lidas de um grupo, com as recusas da leitura: o mínimo e a inclusão do candidato são
/// declarados sempre, e o que falta é recusado no caminho dele, dentro das ocorrências. O máximo ausente
/// é o grupo sem limite.
/// </summary>
/// <param name="Minimo">O mínimo declarado, ou nulo quando ausente.</param>
/// <param name="IncluiCandidato">Se o candidato é uma das ocorrências, ou nulo quando ausente.</param>
/// <param name="Maximo">O máximo declarado, ou nulo quando o grupo não tem limite.</param>
/// <param name="Recusas">As recusas da leitura, no caminho a partir do grupo.</param>
public sealed record OcorrenciasLidasDoGrupo(int? Minimo, bool? IncluiCandidato, int? Maximo, IReadOnlyList<FieldError> Recusas)
{
    private const string MensagemDoMinimo = "O mínimo de ocorrências do grupo é obrigatório.";
    private const string MensagemDaInclusao = "É obrigatório declarar se o candidato é uma das ocorrências do grupo.";

    /// <summary>As ocorrências declaradas; nulas quando a leitura teve alguma recusa.</summary>
    public OcorrenciasDoGrupo? Declaradas => Recusas.Count == 0 ? ParaAConferenciaDaForma : null;

    /// <summary>
    /// As ocorrências lidas, com mínimo zero e sem o candidato no lugar do que faltou, para a contagem ser
    /// conferida mesmo assim: sem mínimo declarado, só o máximo abaixo de um é recusado, e isso não depende do mínimo.
    /// </summary>
    public OcorrenciasDoGrupo ParaAConferenciaDaForma => new(Minimo ?? 0, Maximo, IncluiCandidato ?? false);

    /// <summary>
    /// A identificação da ocorrência do candidato, só quando a inclusão foi declarada verdadeira; o mínimo
    /// ausente não é conferido nela.
    /// </summary>
    public IEnumerable<FieldError> ConferirCandidatoComoMembro(IReadOnlyList<CampoDoMembro> campos) =>
        IncluiCandidato is true ? CandidatoComoMembro.Conferir(Minimo, campos) : [];

    /// <summary>Lê as ocorrências da entrada; a entrada ausente recusa o mínimo e a inclusão do candidato.</summary>
    public static OcorrenciasLidasDoGrupo Ler(OcorrenciasDoGrupoInput? entrada)
    {
        List<FieldError> recusas = [];
        if (entrada?.Minimo is null)
        {
            recusas.Add(Recusa("minimo", MensagemDoMinimo));
        }

        if (entrada?.IncluiCandidato is null)
        {
            recusas.Add(Recusa("incluiCandidato", MensagemDaInclusao));
        }

        return new OcorrenciasLidasDoGrupo(entrada?.Minimo, entrada?.IncluiCandidato, entrada?.Maximo, recusas);
    }

    private static FieldError Recusa(string campo, string mensagem) =>
        new($"{GrupoColetadoInput.CaminhoDasOcorrencias}.{campo}", new DomainError(GrupoFormularioErrorCodes.OcorrenciasObrigatorias, mensagem));
}
