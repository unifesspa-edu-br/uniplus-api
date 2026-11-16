namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.CriacaoDoFato;

/// <summary>
/// O que depende do fato continuar valendo depois de uma alteração: o agregado contra o seu fato de
/// membro e o derivado por regra contra as suas regras padrão. Só a recusa que a alteração causa
/// conta: a que já existia antes dela não a trava (ADR-0125).
/// </summary>
internal static class ConferenciaDasDependenciasDoFato
{
    /// <summary>
    /// As recusas que <paramref name="aplicar"/> causa no que depende de <paramref name="fato"/>: confere
    /// antes, aplica e confere depois. O que confere de cada eixo só vale com o eixo válido.
    /// </summary>
    internal static List<FieldError> NovasRecusas(
        FatoCandidato fato, CatalogoDeFatos catalogo, Action aplicar, bool pontoValido, bool protecaoValida)
    {
        List<FieldError> antes = [.. Conferir(fato, catalogo, pontoValido: true, protecaoValida: true)];
        aplicar();
        return DiferencaDeRecusas.Novas(Conferir(fato, catalogo, pontoValido, protecaoValida), antes);
    }

    internal static List<FieldError> NovasRecusas(FatoCandidato fato, CatalogoDeFatos catalogo, Action aplicar) =>
        NovasRecusas(fato, catalogo, aplicar, pontoValido: true, protecaoValida: true);

    private static IEnumerable<FieldError> Conferir(FatoCandidato fato, CatalogoDeFatos catalogo, bool pontoValido, bool protecaoValida) =>
        fato.FatoDeMembroAgregado is not null
            ? ContraOMembro(fato, catalogo, pontoValido, protecaoValida)
            : ContraAsRegrasPadrao(fato, catalogo, pontoValido, protecaoValida);

    private static IEnumerable<FieldError> ContraOMembro(FatoCandidato agregado, CatalogoDeFatos catalogo, bool pontoValido, bool protecaoValida)
    {
        FatoCandidato? membro = catalogo.Fatos.FirstOrDefault(f => string.Equals(f.Codigo, agregado.FatoDeMembroAgregado, StringComparison.Ordinal));
        if (membro is null || !(pontoValido || protecaoValida))
        {
            return [];
        }

        List<FieldError> contraOMembro = [];
        ConferirContraOMembro(contraOMembro, membro, agregado.PontoResolucao, agregado.ClassificacaoProtecao, catalogo);
        return contraOMembro.Where(e => e.Field == "pontoResolucao" ? pontoValido : protecaoValida);
    }

    // As regras padrão são as do fato, e a mudança de domínio, de ponto de resolução ou de proteção
    // pode deixá-las sem valer. A recusa vai no eixo que a causou, não na regra.
    private static IEnumerable<FieldError> ContraAsRegrasPadrao(FatoCandidato derivado, CatalogoDeFatos catalogo, bool pontoValido, bool protecaoValida)
    {
        if (!derivado.DerivadoPorRegra || derivado.RegrasPadrao.Count == 0)
        {
            return [];
        }

        Result regras = ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo);
        return regras.Errors.Select(static e => NoEixoQueCausou(e)).Where(e => ComEixoValido(e, pontoValido, protecaoValida));
    }

    private static FieldError NoEixoQueCausou(FieldError recusa) => recusa.Error.Code switch
    {
        FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia => recusa with { Field = "classificacaoProtecao" },
        FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia => recusa with { Field = "pontoResolucao" },
        _ => recusa with { Field = "dominio" },
    };

    private static bool ComEixoValido(FieldError recusa, bool pontoValido, bool protecaoValida) => recusa.Field switch
    {
        "classificacaoProtecao" => protecaoValida,
        "pontoResolucao" => pontoValido,
        _ => true,
    };
}
