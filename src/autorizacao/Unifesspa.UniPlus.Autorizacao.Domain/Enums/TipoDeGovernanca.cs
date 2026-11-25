namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Papel de governança que um perfil de sistema exerce. Só os perfis de governança
/// carregam um valor; os perfis comuns não têm nenhum.
/// </summary>
public enum TipoDeGovernanca
{
    /// <summary>Opera as concessões: concede, revoga, mantém os perfis e publica o termo de responsabilidade.</summary>
    [CodigoDeContrato("GESTOR_DE_ACESSOS", "Gestor de acessos")]
    GestorDeAcessos = 1,

    /// <summary>
    /// Decide, em nome da instituição, a segunda etapa dos pedidos com dado pessoal ou sensível, as versões protegidas,
    /// a revisão das concessões e a proteção do catálogo (ADR-0162).
    /// </summary>
    [CodigoDeContrato("REPRESENTANTE_DO_CONTROLADOR", "Representante do controlador")]
    RepresentanteDoControlador = 2,

    /// <summary>
    /// Acompanha o tratamento de dados pessoais na gestão de acessos: consulta, registra manifestação técnica e solicita
    /// providências, sem aprovar, recusar nem revogar (ADR-0162).
    /// </summary>
    [CodigoDeContrato("GESTOR_DE_PROTECAO_DE_DADOS", "Gestor de proteção de dados")]
    GestorDeProtecaoDeDados = 3,
}
