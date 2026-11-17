namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O que a publicação e a retificação de ato único fazem com os motivos escolhidos: a escolha que entra na
/// versão que nasce tem de vir de motivo ativo (UNI-REQ-0122) e copia a descrição vigente do cadastro. A escolha que já
/// entrou em versão anterior não muda, nem quando o motivo de origem foi desativado depois.
/// </summary>
public static class MotivosAoCongelar
{
    /// <summary>Recopia as escolhas que entram na versão que nasce, ou recusa em lote as que vieram de motivo desativado.</summary>
    public static Result Recopiar(IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos, int numeroDaVersaoQueNasce)
    {
        ArgumentNullException.ThrowIfNull(escolhidos);

        MotivoEscolhidoComOrigem[] queEntram = [.. escolhidos.Where(e => e.Escolhido.PendenteNa(numeroDaVersaoQueNasce))];
        FieldError[] desativados = [.. queEntram.Where(static e => e.Origem is not { Ativo: true }).Select(Desativado)];
        if (desativados.Length > 0)
        {
            return Result.ValidationFailure(desativados);
        }

        Array.ForEach(queEntram, static e => e.Escolhido.Recopiar(e.Origem!));

        return Result.Success();
    }

    private static FieldError Desativado(MotivoEscolhidoComOrigem escolhido) =>
        new("motivos", new DomainError(
            MotivoDeDecisaoErrorCodes.MotivoDesativado,
            $"O motivo {escolhido.Escolhido.Codigo.Valor} foi desativado no cadastro depois de escolhido; troque-o antes de publicar."));
}
