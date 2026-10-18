namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// As partes da requisição de registro do ato que são iguais para o ato que cria versão e para o
/// ato avulso: a quem o ato se vincula e o que o catálogo dizia do tipo quando ele foi conferido.
/// </summary>
internal static class RequisicaoDeRegistroDoAto
{
    public static VinculoEntidadeRequisicao VinculoDoProcesso(Guid processoSeletivoId) =>
        new(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, processoSeletivoId);

    /// <summary>
    /// Os atributos como o catálogo os declarava na conferência — copiados por valor (ADR-0061). O
    /// registro acontece depois, e o cadastro é editável: relê-lo faria a decisão que devolveu a
    /// resposta valer outra coisa.
    /// </summary>
    public static AtributosDoTipoAto AtributosDoTipo(TipoAtoPublicadoView tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new AtributosDoTipoAto(tipo.CongelaConfiguracao, tipo.UnicoPorObjeto, tipo.EfeitoIrreversivel, tipo.Nome);
    }
}
