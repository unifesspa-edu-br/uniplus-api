namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Situação do acesso do perfil de acesso, e não de cada concessão. Com o perfil em vigor, uma concessão ainda pode não
/// dar acesso: prazo vencido, revisão das concessões em andamento ou permissão suspensa por reclassificação. A desativação
/// do perfil não altera a situação.
/// </summary>
[DescricaoDoContrato("Situação do acesso do perfil, e não de cada concessão: com o perfil em vigor, uma concessão ainda pode não dar acesso por prazo vencido, revisão das concessões em andamento ou permissão suspensa por reclassificação. A desativação do perfil não altera a situação.")]
public enum SituacaoDoAcessoDoPerfil
{
    [CodigoDeContrato("SUSPENSO", "Suspenso", Descricao = "Há suspensão aberta: nenhuma concessão do perfil dá acesso até a retomada.")]
    Suspenso = 1,

    [CodigoDeContrato("EM_VIGOR", "Em vigor", Descricao = "Sem suspensão e com permissões valendo. O perfil de governança está sempre em vigor.")]
    EmVigor = 2,

    [CodigoDeContrato("SEM_VERSAO_EM_VIGOR", "Sem versão em vigor", Descricao = "Sem suspensão e sem versão aprovada valendo.")]
    SemVersaoEmVigor = 3,
}
