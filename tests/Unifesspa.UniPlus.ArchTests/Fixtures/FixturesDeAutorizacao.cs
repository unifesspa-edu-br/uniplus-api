namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Controllers mínimos que servem de contraprova às regras de autorização: cada regra tem de
/// rejeitar o que está marcado como violação e aceitar o que está marcado como válido.
/// </summary>
internal static class FixturesDeAutorizacao
{
    [Authorize(Policy = "PodeAprovarEdital")]
    internal sealed class PoliticaDeNegocioNaClasse
    {
        [HttpGet]
        public int Ler()
        {
            return GetHashCode();
        }
    }

    internal sealed class PoliticaDeNegocioNaAcao
    {
        [Authorize(Policy = "CoordenadorDaUnidade")]
        [HttpPost]
        public int Aprovar()
        {
            return GetHashCode();
        }
    }

    [Authorize]
    internal sealed class SoAutenticacao
    {
        [HttpGet]
        public int Ler()
        {
            return GetHashCode();
        }
    }

    internal sealed class PoliticaDeIdentidade
    {
        [Authorize(Policy = "Candidato")]
        [HttpGet]
        public int Ler()
        {
            return GetHashCode();
        }
    }

    internal sealed class PapelFixoDaUnidade
    {
        [Authorize(Roles = "coordenador-ceps")]
        public int Aprovar()
        {
            return GetHashCode();
        }
    }

    [Authorize]
    internal sealed class AcaoAdministrativaSoAutenticada
    {
        [HttpPost]
        public int Publicar()
        {
            return GetHashCode();
        }
    }

    internal sealed class AcaoAdministrativaSemAtributo
    {
        [HttpPost]
        public int Publicar()
        {
            return GetHashCode();
        }
    }

    [Authorize]
    internal sealed class AcaoAdministrativaDecididaPeloPontoUnico
    {
        [Unifesspa.UniPlus.Selecao.API.Authorization.ExigePermissaoNoProcesso(Unifesspa.UniPlus.Selecao.Application.Permissoes.PermissaoSelecao.ProcessosConfigurar)]
        [HttpPost]
        public int Publicar()
        {
            return GetHashCode();
        }
    }
}
