namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

/// <summary>Códigos de recusa da lista de assinantes de uma fase, ao configurar e ao emitir o documento.</summary>
public static class AssinantesErrorCodes
{
    public const string Repetido = "Assinantes.Repetido";
    public const string NaoCadastrado = "Assinantes.NaoCadastrado";
    public const string Inativo = "Assinantes.Inativo";
    public const string SemAceiteVigente = "Assinantes.SemAceiteVigente";
    public const string SemAutoridadePelaUnidadeDaFase = "Assinantes.SemAutoridadePelaUnidadeDaFase";
    public const string EmissaoSemAssinantes = "Assinantes.EmissaoSemAssinantes";
    public const string HierarquiaComCiclo = "Assinantes.HierarquiaComCiclo";
}
