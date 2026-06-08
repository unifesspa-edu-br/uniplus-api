namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Indica como o registro de Unidade foi gerado no sistema.
/// </summary>
public enum OrigemUnidade
{
    Nenhum = 0,
    LegadoCoc = 1,
    CriadoNoUniPlus = 2,
    ImportadoSiorg = 3,
}
