namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Reconfere, no momento do congelamento, que cada <see cref="FatoColetado"/> do processo ainda
/// satisfaz <see cref="ColetabilidadeDeFato.EhColetavel"/> contra o catálogo VIVO — o mesmo
/// predicado que <see cref="DefinirFatosColetadosCommandHandler"/> já aplica ao vincular o fato
/// (PUT /fatos-coletados), reaplicado aqui porque o catálogo pode reclassificar a
/// <c>Origem</c> de um fato depois que ele já virou <see cref="FatoColetado"/> — a migration que
/// reclassificou MODALIDADE de DECLARADO para DERIVADO é o caminho real que o projeto usa para
/// evoluir o catálogo. Sem esta reconferência, um vínculo morto seria congelado numa versão
/// append-only e, pela doutrina do agregado, irreparável depois.
/// </summary>
/// <remarks>
/// Espelha <see cref="ResolvedorMetadadosFatosCongelados"/> em estilo: helper estático
/// compartilhado pelos três handlers que congelam. Não faz I/O próprio — recebe o catálogo
/// inteiro (baixo volume) já lido UMA vez pelo handler, compartilhado com o gate de valor
/// inativo e os dois resolvedores: duas leituras abririam janela para um gate aprovar sobre um
/// catálogo e outro passo congelar sobre outro.
/// </remarks>
internal static class ConferenciaDeColetabilidadeDeFatos
{
    public const string FatoColetadoNaoMaisDeclarado = "ProcessoSeletivo.FatoColetadoNaoMaisDeclarado";

    public static Result Conferir(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        if (processo.FatosColetados.Count == 0)
        {
            return Result.Success();
        }

        FatoColetado? naoColetavel = processo.FatosColetados
            .OrderBy(static f => f.Ordem)
            .FirstOrDefault(f => !catalogo.TryGetValue(f.FatoCodigo, out FatoCandidatoView? fato)
                || !ColetabilidadeDeFato.EhColetavel(fato));

        if (naoColetavel is not null)
        {
            return Result.Failure(new DomainError(
                FatoColetadoNaoMaisDeclarado,
                $"O fato coletado '{naoColetavel.FatoCodigo}' não é mais declarado/vinculado a campo de " +
                "inscrição no catálogo de fatos do candidato — o catálogo mudou depois que este fato foi " +
                "vinculado ao formulário, e o congelamento não persiste um vínculo que já não é coletável."));
        }

        return Result.Success();
    }
}
