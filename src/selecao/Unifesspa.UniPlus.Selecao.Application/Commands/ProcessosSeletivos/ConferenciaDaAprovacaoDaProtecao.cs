namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// A publicação e a retificação só congelam proteção que o Representante do controlador aprovou (ADR-0153): o tipo de
/// documento de cada exigência e o dado do candidato que o processo usa — coletado, citado por regra, gatilho ou
/// formulário, derivado por ele ou comprovado por uma exigência — precisam estar aprovados no catálogo de hoje. O item
/// que voltou a pendente depois de vinculado também barra, inclusive na retificação, e se resolve pela aprovação ou pelo
/// aceite da recomendação. O tipo exigido que o cadastro não devolve mais (excluído) não passa como aprovado. Cada item
/// é recusado no próprio campo, juntos, e a mensagem o nomeia como o catálogo o nomeia.
/// </summary>
/// <remarks>
/// O dado derivado que nenhum item do processo usa continua no envelope, congelado como a versão vigente o tem, mas não
/// bloqueia: o que barra é a proteção que o processo de fato depende.
/// </remarks>
internal static class ConferenciaDaAprovacaoDaProtecao
{
    private const string OrientacaoDaAprovacao =
        "Peça ao administrador do catálogo que envie a solicitação de aprovação ou, se houver ajuste recomendado, que aceite a recomendação; ";

    /// <summary>
    /// As causas como a publicação e a retificação as devolvem: a aprovação é do cadastro (o tipo de documento e o dado
    /// do candidato), e não do documento da configuração, então chegam sem campo, e a mensagem diz o tipo ou o dado que
    /// aguarda a aprovação.
    /// </summary>
    public static IReadOnlyList<FieldError> CausasAoCongelar(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos,
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos) =>
        [.. Conferir(processo, tiposVivos, catalogoDeFatos).Errors.Select(static causa => causa with { Field = null })];

    public static Result Conferir(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos,
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(tiposVivos);
        ArgumentNullException.ThrowIfNull(catalogoDeFatos);

        List<FieldError> erros =
        [
            .. RecusasDosTiposDeDocumentoExigidos(processo, tiposVivos),
            .. RecusasDosDadosUsadosPeloProcesso(processo, catalogoDeFatos),
        ];

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    private static IEnumerable<FieldError> RecusasDosTiposDeDocumentoExigidos(
        ProcessoSeletivo processo, IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos)
    {
        List<Guid> exigidos = [.. processo.DocumentosExigidos.Select(static d => d.TipoDocumentoOrigemId).Distinct()];
        IEnumerable<FieldError> naoEncontrados = exigidos.Where(id => !tiposVivos.ContainsKey(id)).Select(TipoExigidoNaoEncontrado);
        IEnumerable<FieldError> pendentes = exigidos.Where(tiposVivos.ContainsKey).Select(id => tiposVivos[id])
            .Where(static tipo => !tipo.ProtecaoAprovada)
            .OrderBy(static tipo => tipo.Codigo, StringComparer.Ordinal)
            .Select(TipoExigidoPendente);
        return [.. naoEncontrados, .. pendentes];
    }

    private static IEnumerable<FieldError> RecusasDosDadosUsadosPeloProcesso(
        ProcessoSeletivo processo, IReadOnlyList<FatoCandidatoView> catalogoDeFatos)
    {
        HashSet<string> usados = new(ReferenciasVivas.Vinculos(processo.Declaracao()).Fatos, StringComparer.Ordinal);
        usados.UnionWith(processo.DocumentosExigidos.SelectMany(static d => d.FatosComprovados));

        return catalogoDeFatos
            .Where(f => !f.ProtecaoAprovada && usados.Contains(f.Codigo))
            .OrderBy(static f => f.Codigo, StringComparer.Ordinal)
            .Select(DadoUsadoPendente);
    }

    private static FieldError TipoExigidoNaoEncontrado(Guid tipoId) =>
        new($"documentosExigidos.{tipoId}", new DomainError(
            ProcessoSeletivoErrorCodes.TipoDocumentoDoVinculoNaoEncontrado,
            $"O tipo de documento {tipoId} exigido pelo processo não existe mais no cadastro; troque a exigência."));

    private static FieldError TipoExigidoPendente(TipoDocumentoView tipo) =>
        new($"documentosExigidos.{tipo.Codigo}", new DomainError(
            ProcessoSeletivoErrorCodes.TipoDocumentoComProtecaoPendente,
            $"O tipo de documento '{tipo.Nome}', exigido pelo processo, aguarda a aprovação do Representante do controlador, "
            + $"e o processo só pode ser publicado ou retificado depois dela. {OrientacaoDaAprovacao}ou troque o tipo de documento da exigência."));

    private static FieldError DadoUsadoPendente(FatoCandidatoView dado) =>
        new($"fatos.{dado.Codigo}", new DomainError(
            ProcessoSeletivoErrorCodes.FatoComProtecaoPendente,
            $"O dado do candidato '{dado.Nome}', usado pelo processo, aguarda a aprovação do Representante do controlador, "
            + $"e o processo só pode ser publicado ou retificado depois dela. {OrientacaoDaAprovacao}"
            + "ou retire o dado do formulário, da regra, da condição ou da exigência de documento que o usa."));
}
