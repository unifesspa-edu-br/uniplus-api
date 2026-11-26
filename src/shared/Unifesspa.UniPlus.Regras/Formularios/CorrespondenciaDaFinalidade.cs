namespace Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A tradução entre a finalidade que o formulário ainda grava e a declaração que as regras de forma
/// leem (ADR-0171). É o único ponto que conhece a correspondência: a inscrição serve à fase de
/// inscrição e é obrigatória, a habilitação é obrigatória sem servir a essa fase e a isenção da
/// taxa não é nenhuma das duas.
/// </summary>
public static class CorrespondenciaDaFinalidade
{
    /// <summary>A declaração que corresponde à finalidade.</summary>
    public static DeclaracaoDoFormulario DeclaracaoDa(FinalidadeFormulario finalidade) => finalidade switch
    {
        FinalidadeFormulario.Inscricao => new(ServeAFaseDeInscricao: true, Obrigatorio: true),
        FinalidadeFormulario.IsencaoTaxa => new(ServeAFaseDeInscricao: false, Obrigatorio: false),
        FinalidadeFormulario.Habilitacao => new(ServeAFaseDeInscricao: false, Obrigatorio: true),
        _ => throw new ArgumentOutOfRangeException(nameof(finalidade), finalidade, "Finalidade de formulário desconhecida."),
    };

    /// <summary>A declaração que corresponde à finalidade; sem finalidade, o formulário não declara nada.</summary>
    public static DeclaracaoDoFormulario? DeclaracaoDa(FinalidadeFormulario? finalidade) =>
        finalidade is { } declarada ? DeclaracaoDa(declarada) : null;

    /// <summary>A finalidade que corresponde à declaração: serve à inscrição, é inscrição; senão, obrigatório é habilitação.</summary>
    public static FinalidadeFormulario FinalidadeDa(DeclaracaoDoFormulario declaracao) =>
        declaracao.ServeAFaseDeInscricao
            ? FinalidadeFormulario.Inscricao
            : declaracao.Obrigatorio ? FinalidadeFormulario.Habilitacao : FinalidadeFormulario.IsencaoTaxa;
}
