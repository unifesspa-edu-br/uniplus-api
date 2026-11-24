namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a identificação precisa ter para ser publicada: o tipo de processo, a unidade administradora e a origem dos candidatos, na
/// ordem do bloco do documento. O rascunho do documento salva sem eles, e a publicação os exige. O que a leitura do documento recusou
/// (um código que o cadastro não tem) não está ausente: a causa é a da leitura, e o item do painel fica pendente em vez de aprovado.
/// </summary>
internal static class CompletudeDaIdentificacao
{
    internal static IReadOnlyList<FieldError> Recusas(ConfiguracaoDeclarada configuracao) =>
    [
        .. Exigencias(configuracao)
            .Where(static exigencia => exigencia.Ausente && !exigencia.Recusada)
            .Select(static exigencia => new FieldError(exigencia.Campo, new DomainError(exigencia.Codigo, exigencia.Mensagem))),
    ];

    internal static IReadOnlyList<ItemConformidade> Itens(ConfiguracaoDeclarada configuracao) =>
    [
        .. Exigencias(configuracao)
            .Select(static exigencia => new ItemConformidade(exigencia.CodigoDoItem, DimensaoConformidade.Identificacao, exigencia.Rotulo, !exigencia.Ausente && !exigencia.Recusada)),
    ];

    private static Exigencia[] Exigencias(ConfiguracaoDeclarada configuracao) =>
        [TipoDeProcesso(configuracao), UnidadeAdministradora(configuracao), OrigemDosCandidatos(configuracao)];

    private static Exigencia TipoDeProcesso(ConfiguracaoDeclarada configuracao) => new(
        ProcessoSeletivoErrorCodes.TipoProcessoAusente,
        "tipo_processo_nao_declarado",
        IdentidadeDoProcessoDeclarada.CampoDoTipoDeProcesso,
        "Tipo de processo do certame",
        "O tipo de processo é obrigatório para gerar versão publicada.",
        configuracao.Identidade.TipoProcesso is null,
        configuracao.Origem.TemParteRecusadaEm(IdentidadeDoProcessoDeclarada.CampoDoTipoDeProcesso));

    private static Exigencia UnidadeAdministradora(ConfiguracaoDeclarada configuracao) => new(
        ProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente,
        "unidade_administradora_nao_declarada",
        IdentidadeDoProcessoDeclarada.CampoDaUnidadeAdministradora,
        "Unidade administradora do certame",
        "A unidade administradora é obrigatória para gerar versão publicada.",
        configuracao.Identidade.UnidadeAdministradora is null,
        configuracao.Origem.TemParteRecusadaEm(IdentidadeDoProcessoDeclarada.CampoDaUnidadeAdministradora));

    private static Exigencia OrigemDosCandidatos(ConfiguracaoDeclarada configuracao) => new(
        ProcessoSeletivoErrorCodes.OrigemCandidatosAusente,
        "origem_candidatos_nao_declarada",
        IdentidadeDoProcessoDeclarada.CampoDaOrigemDosCandidatos,
        "Origem dos candidatos do certame",
        "A origem dos candidatos é obrigatória para gerar versão publicada.",
        configuracao.Identidade.OrigemCandidatos is null,
        Recusada: false);

    private sealed record Exigencia(string Codigo, string CodigoDoItem, string Campo, string Rotulo, string Mensagem, bool Ausente, bool Recusada);
}
