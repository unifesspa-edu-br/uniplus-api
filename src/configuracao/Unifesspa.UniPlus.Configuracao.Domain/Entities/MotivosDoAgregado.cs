namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;
using static Unifesspa.UniPlus.Configuracao.Domain.Entities.CriacaoDoFato;

/// <summary>
/// O que o agregado deixa de ser quando o fato de membro passa a ser outro: o domínio
/// e a fonte que ele gravou são os inferidos do membro, e ele não protege menos nem resolve antes dele.
/// </summary>
internal static class MotivosDoAgregado
{
    /// <summary>Vazio quando o agregado continua valendo.</summary>
    internal static IReadOnlyList<FieldError> PelosQuaisDeixaDeAgregar(FatoCandidato agregado, FatoCandidato membro, CatalogoDeFatos catalogo)
    {
        OperacaoAgregado? operacao = AgregadoDeGrupo.OperacaoDoDominio(membro.Dominio);
        if (membro is not { Escopo: EscopoFato.MembroGrupo, Origem: OrigemFato.Declarado })
        {
            return [Motivo("membro", $"'{membro.Codigo}' deixa de ser fato declarado de membro de grupo, e o agregado resume o que cada membro respondeu.")];
        }

        if (operacao is not { } definida)
        {
            return [Motivo("dominioSemResumo", $"'{membro.Codigo}' passa a um domínio que o agregado não resume (só booleano e categórico).")];
        }

        return [.. MotivoDoMembroInativo(membro), .. MotivoDosEixos(agregado, membro, definida), .. MotivosContraOMembro(agregado, membro, catalogo)];
    }

    private static FieldError Motivo(string motivo, string mensagem) =>
        new($"agregado.{motivo}", new DomainError(FatoCandidatoErrorCodes.AlteracaoQuebraAgregadoDoFato, mensagem));

    private static IEnumerable<FieldError> MotivoDoMembroInativo(FatoCandidato membro) =>
        membro.Ativo
            ? []
            : [Motivo("membroInativo", $"'{membro.Codigo}' está desativado no catálogo, e o agregado resume um fato de membro ativo.")];

    // O primeiro eixo que deixou de acompanhar o inferido do membro basta para o motivo.
    private static IEnumerable<FieldError> MotivoDosEixos(FatoCandidato agregado, FatoCandidato membro, OperacaoAgregado operacao)
    {
        (TipoDoDado dominioEsperado, _, FonteValoresFato? fonteEsperada) =
            EixosInferidosDoAgregado(operacao, membro.FonteValores);
        return Presentes(
            agregado.Dominio != dominioEsperado
                ? Motivo("dominio", $"o domínio de '{membro.Codigo}' mudou, e o agregado gravou o domínio {agregado.Dominio.Codigo()} inferido dele.")
                : null,
            agregado.FonteValores != fonteEsperada
                ? Motivo("fonte", $"a fonte dos valores de '{membro.Codigo}' mudou, e o agregado gravou a fonte inferida dela.")
                : null).Take(1);
    }

    private static List<FieldError> MotivosContraOMembro(FatoCandidato agregado, FatoCandidato membro, CatalogoDeFatos catalogo)
    {
        List<FieldError> erros = [];
        ConferirContraOMembro(erros, membro, agregado.PontoResolucao, agregado.ClassificacaoProtecao, catalogo);
        return erros;
    }
}
