namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O identificador legível que a versão publicada vigente já congelou não muda mais: o endereço da página pública e a chave do
/// documento no acervo derivam dele, e o público já o conhece. Quando a versão vigente não o traz, o documento o declara livremente.
/// A unicidade entre processos depende de consulta e não é desta conferência.
/// </summary>
public static class ConferenciaDoIdentificadorDaRetificacao
{
    public const string Campo = "identificacao.identificadorLegivel";

    /// <param name="daVersaoVigente">O identificador que a versão publicada vigente congelou, quando ela o traz.</param>
    /// <param name="doDocumento">O identificador que o documento novo declara.</param>
    public static IReadOnlyList<FieldError> Conferir(IdentificadorLegivel? daVersaoVigente, IdentificadorLegivel? doDocumento) =>
        daVersaoVigente is { } congelado && congelado != doDocumento
            ? [new FieldError(Campo, new DomainError(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel,
                "O identificador legível já consta em versão publicada e não pode ser alterado."))]
            : [];
}
