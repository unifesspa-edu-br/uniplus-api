namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;

using FluentValidation;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;

/// <summary>
/// Regras FluentValidation reutilizáveis do <see cref="EnderecoGeoInput"/> para
/// os validators dos cadastros (CA-03/CA-04), delegando ao value object
/// <see cref="ReferenciaEnderecoGeo"/> sem duplicar lógica.
/// </summary>
internal static class EnderecoGeoInputValidation
{
    /// <summary>
    /// Adiciona, quando há endereço no payload, as regras de formato e de
    /// coerência cidade↔CEP, lendo o endereço e a cidade do nível raiz do comando
    /// via os seletores informados.
    /// </summary>
    public static void RegrasDeEndereco<T>(
        this AbstractValidator<T> validator,
        Func<T, EnderecoGeoInput?> endereco,
        Func<T, string?> cidadeCodigoIbge,
        Func<T, string?> cidadeUf)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(endereco);

        validator.When(x => endereco(x) is not null, () =>
        {
            validator.RuleFor(x => x)
                .Must(x => FormatoValido(endereco(x)!))
                .WithMessage("Endereço inválido: verifique CEP (8 dígitos), cidade, nível de resolução e origem.")
                .Must(x => CoerenteComCidade(endereco(x), cidadeCodigoIbge(x), cidadeUf(x)))
                .WithMessage("A cidade do endereço (resolvida pelo CEP) deve coincidir com a cidade informada.");
        });
    }

    private static bool FormatoValido(EnderecoGeoInput endereco) =>
        ReferenciaEnderecoGeo.EhValido(
            endereco.Cep,
            endereco.Logradouro,
            endereco.Numero,
            endereco.Complemento,
            endereco.Bairro,
            endereco.Distrito,
            endereco.Cidade?.CodigoIbge,
            endereco.Cidade?.Nome,
            endereco.Cidade?.Uf,
            endereco.Latitude,
            endereco.Longitude,
            endereco.NivelResolucao,
            endereco.Origem);

    private static bool CoerenteComCidade(EnderecoGeoInput? endereco, string? cidadeCodigoIbge, string? cidadeUf) =>
        ReferenciaEnderecoGeo.ValidarCoerencia(
            endereco?.Cidade?.CodigoIbge, endereco?.Cidade?.Uf, cidadeCodigoIbge, cidadeUf).IsSuccess;
}
