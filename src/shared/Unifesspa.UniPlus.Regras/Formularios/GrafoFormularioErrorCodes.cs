namespace Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Códigos de recusa do grafo do formulário, no processo e no modelo.</summary>
public static class GrafoFormularioErrorCodes
{
    public const string FatoDuplicado = "GrafoFormulario.FatoDuplicado";
    public const string OrdemDuplicada = "GrafoFormulario.OrdemDuplicada";
    public const string GrafoComCiclo = "GrafoFormulario.GrafoComCiclo";
    public const string CitaFatoNaoConhecido = "GrafoFormulario.CitaFatoNaoConhecido";
    public const string CitaFatoPosterior = "GrafoFormulario.CitaFatoPosterior";
    public const string CitaAtributoDoCandidato = "GrafoFormulario.CitaAtributoDoCandidato";
    public const string CitaFatoDeMembroForaDoGrupo = "GrafoFormulario.CitaFatoDeMembroForaDoGrupo";
}
