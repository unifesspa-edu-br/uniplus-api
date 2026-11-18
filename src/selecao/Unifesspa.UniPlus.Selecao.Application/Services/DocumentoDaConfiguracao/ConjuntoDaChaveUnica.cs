namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>O conjunto dentro do qual uma chave é única: as partes do mesmo tipo, no documento todo ou sob a mesma parte ancestral.</summary>
/// <param name="Tipo">O tipo da parte.</param>
/// <param name="ParteAncestral">O ponteiro da parte ancestral que delimita o conjunto, ou vazio quando o conjunto é o documento.</param>
internal readonly record struct ConjuntoDaChaveUnica(string Tipo, string ParteAncestral)
{
    public static ConjuntoDaChaveUnica De(NoDoDocumento parte)
    {
        ParteComChave tipo = parte.Forma.Parte!;
        return new ConjuntoDaChaveUnica(tipo.Tipo, tipo.Escopo == EscopoDaChave.ParteAncestral ? parte.ParteAncestral : string.Empty);
    }
}
