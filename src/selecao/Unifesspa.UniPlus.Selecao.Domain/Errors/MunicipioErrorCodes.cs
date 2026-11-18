namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro do município que o documento cita, compartilhados com a conferência de endereço no Geo.</summary>
public static class MunicipioErrorCodes
{
    /// <summary>O código IBGE do município não existe no Geo.</summary>
    public const string NaoEncontrado = "ConferenciaEnderecoGeo.MunicipioNaoEncontrado";
}
