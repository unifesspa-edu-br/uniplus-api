namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Reconfere, no momento do congelamento, que cada <see cref="FatoColetado"/> do processo ainda
/// satisfaz <see cref="ConferenciaNoCatalogo.EhColetavel"/> contra o catálogo VIVO — o mesmo
/// predicado que <see cref="DefinirFatosColetadosCommandHandler"/> já aplica ao vincular o fato
/// (PUT dos itens do formulário), reaplicado aqui porque o catálogo pode reclassificar a
/// <c>Origem</c> de um fato depois que ele já virou <see cref="FatoColetado"/> — a migration que
/// reclassificou MODALIDADE de DECLARADO para CALCULADO é o caminho real que o projeto usa para
/// evoluir o catálogo. Sem esta reconferência, um vínculo morto seria congelado numa versão
/// append-only e, pela doutrina do agregado, irreparável depois.
/// </summary>
/// <remarks>
/// Espelha <see cref="ResolvedorMetadadosFatosCongelados"/> em estilo: helper estático
/// compartilhado pelos três handlers que congelam. Não faz I/O próprio — recebe o catálogo
/// inteiro (baixo volume) já lido UMA vez pelo handler, compartilhado com os dois resolvedores:
/// duas leituras abririam janela para a conferência aprovar sobre um catálogo e outro passo
/// congelar sobre outro.
/// </remarks>
internal static class ConferenciaDeColetabilidadeDeFatos
{
    public const string FatoColetadoNaoMaisDeclarado = "ProcessoSeletivo.FatoColetadoNaoMaisDeclarado";

    public static Result Conferir(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        IReadOnlyList<FieldError> recusas = Recusas(NaoColetaveis(processo, catalogo));

        return recusas.Count > 0 ? Result.ValidationFailure(recusas) : Result.Success();
    }

    /// <summary>
    /// Todo fato coletado que o catálogo não deixa coletar, com o caminho dele no documento, na ordem do documento: por formulário,
    /// os itens e depois os campos dos grupos repetíveis, que coletam fato de membro (UNI-REQ-0146).
    /// </summary>
    public static IReadOnlyList<ItemNoDocumento> NaoColetaveis(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return
        [
            .. ItensDoFormularioNoDocumento.De(processo).SelectMany(formulario => NaoColetaveisDoFormulario(formulario, catalogo)),
        ];
    }

    private static IEnumerable<ItemNoDocumento> NaoColetaveisDoFormulario(
        FormularioComOsSeusItens formulario, IReadOnlyDictionary<string, FatoCandidatoView> catalogo) =>
    [
        .. formulario.Itens.Where(i => !ColetavelNoCatalogo(i.Item, catalogo, ConferenciaNoCatalogo.EhColetavel)),
        .. formulario.Grupos.SelectMany(static g => g.Campos).Where(c => !ColetavelNoCatalogo(c.Item, catalogo, ConferenciaNoCatalogo.EhColetavelEmGrupo)),
    ];

    private static bool ColetavelNoCatalogo(
        FatoColetado coletado,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        Func<FatoDoCatalogo, bool> coletavel) =>
        catalogo.TryGetValue(coletado.FatoCodigo, out FatoCandidatoView? fato) && coletavel(VocabularioDeFatos.ParaRegras(fato));

    /// <summary>Uma causa por fato que o catálogo deixou de aceitar, no campo do documento que o cita, na ordem recebida.</summary>
    public static IReadOnlyList<FieldError> Recusas(IEnumerable<ItemNoDocumento> naoColetaveis) =>
        [.. naoColetaveis.Select(static n => new FieldError($"{n.Caminho}.fatoCodigo", new DomainError(
            FatoColetadoNaoMaisDeclarado,
            $"O fato coletado '{n.Item.FatoCodigo}' não é mais coletável no catálogo de fatos do " +
            "candidato — declarado, do próprio candidato e respondido em campo —; o catálogo mudou depois que este fato foi " +
            "vinculado ao formulário, e o congelamento não persiste um vínculo que já não é coletável.")))];
}
