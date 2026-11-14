namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using System.Reflection;

/// <summary>
/// Os assemblies de produto da solução, cada um por um tipo-âncora: a lista é explícita, e o assembly novo entra
/// aqui junto da referência de projeto que o traz ao projeto de arquitetura.
/// </summary>
internal static class AssembliesDoProduto
{
    internal static readonly Assembly[] Todos =
    [
        typeof(Unifesspa.UniPlus.Application.Abstractions.Authentication.IRequiredUserContext).Assembly,
        typeof(Unifesspa.UniPlus.Authorization.Abstractions.CatalogoDesatualizadoException).Assembly,
        typeof(Unifesspa.UniPlus.Governance.Contracts.IInstituicaoReader).Assembly,
        typeof(Unifesspa.UniPlus.Infrastructure.Core.Authentication.AuthenticationProblemDetailsWriter).Assembly,
        typeof(Unifesspa.UniPlus.Kernel.Domain.Cidades.CidadeReferenciaErrorCodes).Assembly,
        typeof(Unifesspa.UniPlus.Regras.Entradas.EntradaDeRegras).Assembly,
        typeof(Unifesspa.UniPlus.Host.DesignTimeServices).Assembly,
        typeof(Unifesspa.UniPlus.Verificador.Antivirus.ClamdClient).Assembly,
        typeof(Unifesspa.UniPlus.Verificador.Contratos.DestinoDoResultado).Assembly,
        typeof(Unifesspa.UniPlus.Autorizacao.API.AutorizacaoApiAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Autorizacao.Application.Abstractions.IArmazenamentoDosDocumentosDaAutorizacao).Assembly,
        typeof(Unifesspa.UniPlus.Autorizacao.Domain.Entities.AceiteDoTermo).Assembly,
        typeof(Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo.CatalogoDePermissoesDaAplicacao).Assembly,
        typeof(Unifesspa.UniPlus.Configuracao.API.ConfiguracaoApiAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Configuracao.Application.ConfiguracaoApplicationAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Configuracao.Contracts.BaseLegalBonusRegionalView).Assembly,
        typeof(Unifesspa.UniPlus.Configuracao.Domain.ConfiguracaoDomainAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Configuracao.Infrastructure.ConfiguracaoInfrastructureRegistration).Assembly,
        typeof(Unifesspa.UniPlus.Discentes.API.DiscentesApiAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Discentes.Application.DiscentesApplicationAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Discentes.Domain.Entities.FalhaAoPrepararLoteException).Assembly,
        typeof(Unifesspa.UniPlus.Discentes.Infrastructure.DiscentesInfrastructureRegistration).Assembly,
        typeof(Unifesspa.UniPlus.Ingresso.API.IngressoApiAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Ingresso.Domain.Entities.Chamada).Assembly,
        typeof(Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.IngressoContribuicao).Assembly,
        typeof(Unifesspa.UniPlus.OrganizacaoInstitucional.API.Controllers.InstituicaoController).Assembly,
        typeof(Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions.IInstituicaoCacheInvalidator).Assembly,
        typeof(Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities.Instituicao).Assembly,
        typeof(Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching.InstituicaoCacheInvalidator).Assembly,
        typeof(Unifesspa.UniPlus.Portal.API.Controllers.CertamePublicoController).Assembly,
        typeof(Unifesspa.UniPlus.Portal.Application.Certames.ComposicaoDaPaginaDoCertame).Assembly,
        typeof(Unifesspa.UniPlus.Portal.Domain.PortalDomainAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Portal.Infrastructure.PortalInfrastructureRegistration).Assembly,
        typeof(Unifesspa.UniPlus.Publicacoes.API.Controllers.AtosNormativosController).Assembly,
        typeof(Unifesspa.UniPlus.Publicacoes.Application.PublicacoesApplicationAssemblyMarker).Assembly,
        typeof(Unifesspa.UniPlus.Publicacoes.Contracts.AtoNormativoRegistrado).Assembly,
        typeof(Unifesspa.UniPlus.Publicacoes.Domain.Entities.AnexoDoAto).Assembly,
        typeof(Unifesspa.UniPlus.Publicacoes.Infrastructure.PublicacoesInfrastructureRegistration).Assembly,
        typeof(Unifesspa.UniPlus.Selecao.API.Authorization.ExigeLeituraDoProcessoAttribute).Assembly,
        typeof(Unifesspa.UniPlus.Selecao.Application.Abstractions.AcervoPublicoIndisponivelException).Assembly,
        typeof(Unifesspa.UniPlus.Selecao.Domain.Entities.AnexoDoAtoDoProcesso).Assembly,
        typeof(Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization.ComparadorLexicograficoDeBytes).Assembly,
    ];
}
