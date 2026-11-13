namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Authorization.Abstractions;

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
        [Authorize(Policy = "Instalacao")]
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

    /// <summary>Serviço de aplicação que decide pelo ponto único: recebe o verificador no construtor.</summary>
    internal sealed class ServicoQueRecebeOVerificador
    {
        public ServicoQueRecebeOVerificador(IVerificadorDeAcesso verificador) => Verificador = verificador;

        public IVerificadorDeAcesso Verificador { get; }
    }

    /// <summary>Serviço de aplicação que não passa pelo ponto único: nada nele chega ao verificador.</summary>
    internal sealed class ServicoQueNaoRecebeOVerificador
    {
        public ServicoQueNaoRecebeOVerificador(TimeProvider relogio) => Relogio = relogio;

        public TimeProvider Relogio { get; }
    }
}
