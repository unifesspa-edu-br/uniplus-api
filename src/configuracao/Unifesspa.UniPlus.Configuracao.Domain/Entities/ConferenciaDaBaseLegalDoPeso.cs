namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// A base legal do Peso por Área é gravada em NFC, a forma em que o processo seletivo a congela: o
/// limite vale para o texto normalizado, que pode ser mais longo que o digitado. Sem isso, uma base
/// legal aceita aqui passaria do limite da cópia congelada.
/// </summary>
internal static class ConferenciaDaBaseLegalDoPeso
{
    private const int TamanhoMaximo = 500;

    public static string? Conferir(List<FieldError> erros, string? baseLegal)
    {
        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            return Recusar(erros, PesoAreaEnemErrorCodes.BaseLegalObrigatoria, "Base legal é obrigatória.");
        }

        SituacaoDoTexto situacao = TextoNormalizavel.TentarNfcDeTextoLivre(baseLegal.Trim(), TamanhoMaximo, out string normalizada);
        return situacao == SituacaoDoTexto.Valido ? normalizada : RecusarTexto(erros, situacao);
    }

    private static string? RecusarTexto(List<FieldError> erros, SituacaoDoTexto situacao) =>
        situacao == SituacaoDoTexto.CaractereInvalido
            ? Recusar(erros, PesoAreaEnemErrorCodes.BaseLegalCaractereInvalido, "Base legal não pode conter o caractere nulo nem caractere que não seja texto.")
            : Recusar(erros, PesoAreaEnemErrorCodes.BaseLegalTamanho, $"Base legal deve ter no máximo {TamanhoMaximo} caracteres.");

    private static string? Recusar(List<FieldError> erros, string codigo, string mensagem)
    {
        erros.Add(new("baseLegal", new DomainError(codigo, mensagem)));
        return null;
    }
}
