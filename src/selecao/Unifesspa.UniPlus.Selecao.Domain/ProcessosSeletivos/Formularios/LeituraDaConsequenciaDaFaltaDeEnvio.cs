namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Lê o código da consequência da falta de envio declarado no formulário. A declaração é opcional: sem código, a falta
/// de envio não tem consequência própria. O código que não é do vocabulário é recusado citando o formulário.
/// </summary>
public static class LeituraDaConsequenciaDaFaltaDeEnvio
{
    public const string Campo = "consequenciaDaFaltaDeEnvio";

    public static Result<ConsequenciaDoDescumprimento?> Ler(string? codigo, FinalidadeFormulario finalidade)
    {
        if (codigo is null)
        {
            return Result<ConsequenciaDoDescumprimento?>.Success(null);
        }

        return CatalogoDe<ConsequenciaDoDescumprimento>.TentarObter(codigo, out ConsequenciaDoDescumprimento valor)
            ? Result<ConsequenciaDoDescumprimento?>.Success(valor)
            : Result<ConsequenciaDoDescumprimento?>.ValidationFailure([new(Campo, new DomainError(
                FormularioProcessoErrorCodes.ConsequenciaDaFaltaDeEnvioInvalida,
                $"A consequência da falta de envio do formulário '{finalidade.Codigo()}' não é uma opção conhecida."))]);
    }
}
