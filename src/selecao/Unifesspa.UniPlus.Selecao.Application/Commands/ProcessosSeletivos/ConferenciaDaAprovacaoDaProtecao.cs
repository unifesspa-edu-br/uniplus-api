namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// A publicação e a retificação só congelam proteção que a proteção de dados aprovou (ADR-0153): o tipo de documento
/// de cada exigência e o fato que o processo usa — coletado, citado por regra, gatilho ou formulário, derivado por
/// ele ou comprovado por uma exigência — precisam estar aprovados no catálogo de hoje. O item que voltou a pendente
/// depois de vinculado também barra, inclusive na retificação, e se resolve pela aprovação ou pelo aceite da
/// recomendação. O tipo exigido que o cadastro não devolve mais (excluído) não passa como aprovado. Cada item é
/// recusado no próprio campo, juntos.
/// </summary>
/// <remarks>
/// O fato derivado que nenhum item do processo usa continua no envelope, congelado como a versão vigente o tem, mas não
/// bloqueia: o que barra é a proteção que o processo de fato depende.
/// </remarks>
internal static class ConferenciaDaAprovacaoDaProtecao
{
    public const string CodigoDoTipoNoVinculo = "DocumentoExigido.TipoDocumentoComProtecaoPendente";
    public const string CodigoDoTipoNaPublicacao = "ProcessoSeletivo.TipoDocumentoComProtecaoPendente";
    public const string CodigoDoFatoNaPublicacao = "ProcessoSeletivo.FatoComProtecaoPendente";
    public const string CodigoDoTipoExcluido = "DocumentoExigido.TipoDocumentoNaoEncontrado";

    public static Result Conferir(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos,
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(tiposVivos);
        ArgumentNullException.ThrowIfNull(catalogoDeFatos);

        List<FieldError> erros = [];

        foreach (Guid tipoId in processo.DocumentosExigidos.Select(static d => d.TipoDocumentoOrigemId).Distinct())
        {
            if (!tiposVivos.TryGetValue(tipoId, out TipoDocumentoView? tipo))
            {
                erros.Add(new($"documentosExigidos.{tipoId}", new DomainError(
                    CodigoDoTipoExcluido,
                    $"O tipo de documento {tipoId} exigido pelo processo não existe mais no cadastro; troque a exigência.")));
            }
            else if (!tipo.ProtecaoAprovada)
            {
                erros.Add(new($"documentosExigidos.{tipo.Codigo}", new DomainError(
                    CodigoDoTipoNaPublicacao,
                    $"O tipo de documento '{tipo.Codigo}' ({tipo.Nome}) aguarda a aprovação da proteção de dados; "
                    + "falta a aprovação, ou o aceite da recomendação, para publicar.")));
            }
        }

        HashSet<string> usados = new(processo.Vinculos().Fatos, StringComparer.Ordinal);
        usados.UnionWith(processo.DocumentosExigidos.SelectMany(static d => d.FatosComprovados));
        foreach (FatoCandidatoView fato in catalogoDeFatos
            .Where(f => !f.ProtecaoAprovada && usados.Contains(f.Codigo))
            .OrderBy(static f => f.Codigo, StringComparer.Ordinal))
        {
            erros.Add(new($"fatos.{fato.Codigo}", new DomainError(
                CodigoDoFatoNaPublicacao,
                $"O fato '{fato.Codigo}' ({fato.Nome}) aguarda a aprovação da proteção de dados; "
                + "falta a aprovação, ou o aceite da recomendação, para publicar.")));
        }

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }
}
