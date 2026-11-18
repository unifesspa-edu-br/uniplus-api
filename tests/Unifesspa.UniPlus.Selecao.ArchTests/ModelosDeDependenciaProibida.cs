namespace Unifesspa.UniPlus.Selecao.ArchTests.Modelos
{
    using Unifesspa.UniPlus.Governance.Modelo;
    using Unifesspa.UniPlus.Infrastructure.Core.Modelo;
    using Unifesspa.UniPlus.OrganizacaoInstitucional.Modelo;

    /// <summary>Tipo de origem que depende de um alvo de cada módulo de fora, para provar o alcance da regra.</summary>
    public sealed record OrigemDaDependenciaProibida(
        UnidadeDoCadastro Cadastro, DecisaoDeGovernanca Governanca, ErroDeInfraestrutura Infraestrutura);
}

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Modelo
{
    public sealed record UnidadeDoCadastro(Guid Id);
}

namespace Unifesspa.UniPlus.Governance.Modelo
{
    public sealed record DecisaoDeGovernanca(Guid Id);
}

namespace Unifesspa.UniPlus.Infrastructure.Core.Modelo
{
    public sealed record ErroDeInfraestrutura(Guid Id);
}
