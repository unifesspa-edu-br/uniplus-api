namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Classificação organizacional de uma Unidade institucional da Unifesspa (ADR-0055).
/// Roster fechado de 11 valores — novos tipos exigem deliberação do Tech Lead.
/// O valor numérico faz parte do contrato: nunca renumerar existentes.
/// </summary>
public enum TipoUnidade
{
    Nenhum = 0,        // Sentinel — indica corrupção se encontrado em runtime
    Reitoria = 1,
    ProReitoria = 2,
    Centro = 3,
    Instituto = 4,
    Faculdade = 5,
    Departamento = 6,
    Coordenacao = 7,
    Diretoria = 8,
    Divisao = 9,
    Nucleo = 10,
    Outro = 11,
}
