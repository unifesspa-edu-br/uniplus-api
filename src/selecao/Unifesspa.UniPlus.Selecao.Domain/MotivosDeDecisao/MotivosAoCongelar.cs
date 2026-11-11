namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O que a publicação, a retificação e o fechamento da sessão fazem com os motivos escolhidos:
/// a escolha ainda não congelada tem de vir de motivo ativo (UNI-REQ-0122) e copia a descrição
/// vigente do cadastro; só então tudo congela. A cópia já congelada não muda, nem quando o motivo
/// de origem foi desativado depois.
/// </summary>
public static class MotivosAoCongelar
{
    /// <summary>Recopia as escolhas pendentes, ou recusa em lote as que vieram de motivo desativado.</summary>
    public static Result Recopiar(IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos)
    {
        ArgumentNullException.ThrowIfNull(escolhidos);

        FieldError[] desativados = [.. escolhidos.Where(static e => !e.Escolhido.Congelado && e.Origem is not { Ativo: true }).Select(Desativado)];
        if (desativados.Length > 0)
        {
            return Result.ValidationFailure(desativados);
        }

        foreach (MotivoEscolhidoComOrigem e in escolhidos)
        {
            e.Escolhido.Recopiar(e.Origem!);
        }

        return Result.Success();
    }

    private static FieldError Desativado(MotivoEscolhidoComOrigem escolhido) =>
        new("motivosDeDecisao", new DomainError(
            MotivoDeDecisaoErrorCodes.MotivoDesativado,
            $"O motivo {escolhido.Escolhido.Codigo.Valor} foi desativado no cadastro depois de escolhido; troque-o antes de publicar."));

    /// <summary>Congela todas as escolhas: daqui em diante elas só se incluem, nunca mudam nem saem.</summary>
    public static void Congelar(IEnumerable<MotivoDoProcesso> escolhidos)
    {
        ArgumentNullException.ThrowIfNull(escolhidos);

        foreach (MotivoDoProcesso escolhido in escolhidos)
        {
            escolhido.Congelar();
        }
    }
}
