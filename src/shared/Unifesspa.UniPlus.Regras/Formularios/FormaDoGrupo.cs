namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// A forma de um grupo repetível do formulário, a mesma no processo e no modelo (UNI-REQ-0146): o
/// código, a ordem, o rótulo, as ocorrências e os campos de cada ocorrência.
/// Acumula toda violação independente em vez de parar na primeira (ADR-0125).
/// </summary>
public static class FormaDoGrupo
{
    public const int CodigoMaxLength = 60;

    /// <summary>Teto de campos de cada ocorrência: um membro da família tem poucos dados declarados.</summary>
    public const int MaximoDeSubitens = 30;

    /// <summary>O grupo tem de um até o teto de campos por ocorrência.</summary>
    public static bool QuantidadeDeCamposValida(int quantidade) => quantidade is >= 1 and <= MaximoDeSubitens;

    /// <summary>
    /// Código e rótulo obrigatórios e limitados, ordem não negativa, ocorrências coerentes, ao menos um
    /// campo e no máximo o teto, nenhum campo com seção própria — o campo aparece onde o grupo aparece
    /// — e nenhuma regra do grupo citando o próprio grupo ou os campos dele — fora da ocorrência, eles
    /// não têm valor único.
    /// </summary>
    public static List<FieldError> Conferir(
        string? codigo,
        int ordem,
        string? rotulo,
        OcorrenciasDoGrupo ocorrencias,
        IReadOnlyList<(string? FatoCodigo, string? EtapaCodigo, bool TemImpedimento)> subitens,
        ExibicaoEObrigatoriedade regras)
    {
        ArgumentNullException.ThrowIfNull(ocorrencias);
        ArgumentNullException.ThrowIfNull(subitens);
        ArgumentNullException.ThrowIfNull(regras);

        return
        [
            .. RecusasDoCodigo(codigo), .. RecusasDaOrdem(ordem), .. RecusasDoRotulo(rotulo),
            .. ocorrencias.Conferir().Select(static erro => erro.NoCaminhoDe(GrupoColetadoInput.CaminhoDasOcorrencias)),
            .. RecusasDaQuantidadeDeCampos(subitens.Count), .. RecusasDosCampos(subitens),
            .. RecusasDeRegraQueCitaOGrupo(codigo?.Trim() ?? string.Empty, subitens, regras),
        ];
    }

    private static IEnumerable<FieldError> RecusasDoCodigo(string? codigo)
    {
        if ((codigo?.Trim() ?? string.Empty).Length is 0 or > CodigoMaxLength)
        {
            yield return new("codigo", new DomainError(
                GrupoFormularioErrorCodes.CodigoInvalido, $"O código do grupo é obrigatório e tem no máximo {CodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(GrupoFormularioErrorCodes.OrdemInvalida, "A ordem do grupo não pode ser negativa."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoRotulo(string? rotulo)
    {
        if ((rotulo?.Trim() ?? string.Empty).Length is 0 or > FormaDoItem.RotuloMaxLength)
        {
            yield return new("rotulo", new DomainError(
                GrupoFormularioErrorCodes.RotuloInvalido,
                $"O rótulo do grupo é obrigatório e tem no máximo {FormaDoItem.RotuloMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaQuantidadeDeCampos(int quantidade)
    {
        if (!QuantidadeDeCamposValida(quantidade))
        {
            yield return new("subitens", new DomainError(
                GrupoFormularioErrorCodes.SubitensForaDoLimite, $"O grupo tem de um a {MaximoDeSubitens} campos por ocorrência."));
        }
    }

    private static IEnumerable<FieldError> RecusasDosCampos(IReadOnlyList<(string? FatoCodigo, string? EtapaCodigo, bool TemImpedimento)> subitens) =>
        subitens.SelectMany(static (subitem, indice) => RecusasDoCampo(subitem, indice));

    private static IEnumerable<FieldError> RecusasDoCampo((string? FatoCodigo, string? EtapaCodigo, bool TemImpedimento) subitem, int indice)
    {
        if (subitem.EtapaCodigo is not null)
        {
            yield return new($"subitens[{indice}].etapaCodigo", new DomainError(
                GrupoFormularioErrorCodes.CampoComSecaoPropria,
                $"O campo '{subitem.FatoCodigo}' segue a seção do grupo e não declara seção própria."));
        }

        if (subitem.TemImpedimento)
        {
            yield return new($"subitens[{indice}].{FatoColetadoInput.CaminhoDasRegras}.impedimento", new DomainError(
                GrupoFormularioErrorCodes.CampoComImpedimento,
                $"O campo '{subitem.FatoCodigo}' é de membro: o impedimento é da inscrição do candidato, não de um membro."));
        }
    }

    private static IEnumerable<FieldError> RecusasDeRegraQueCitaOGrupo(
        string codigoAparado,
        IReadOnlyList<(string? FatoCodigo, string? EtapaCodigo, bool TemImpedimento)> subitens,
        ExibicaoEObrigatoriedade regras)
    {
        HashSet<string> doGrupo = new(subitens.Select(static s => s.FatoCodigo).OfType<string>().Append(codigoAparado), StringComparer.Ordinal);
        return
        [
            .. RecusasDaRegraQueCitaOGrupo($"{GrupoColetadoInput.CaminhoDasRegras}.exibicao", regras.Exibicao?.FatosCitados ?? [], doGrupo),
            .. RecusasDaRegraQueCitaOGrupo(
                $"{GrupoColetadoInput.CaminhoDasRegras}.obrigatoriedade.predicado", regras.Obrigatoriedade.FatosCitados, doGrupo),
        ];
    }

    private static IEnumerable<FieldError> RecusasDaRegraQueCitaOGrupo(string campo, IEnumerable<string> citados, HashSet<string> doGrupo)
    {
        if (citados.FirstOrDefault(doGrupo.Contains) is { } citado)
        {
            yield return new(campo, new DomainError(
                GrupoFormularioErrorCodes.RegraAutorreferente,
                $"Uma regra do grupo cita '{citado}', do próprio grupo — a exibição e a obrigatoriedade do grupo se decidem antes das ocorrências."));
        }
    }
}
