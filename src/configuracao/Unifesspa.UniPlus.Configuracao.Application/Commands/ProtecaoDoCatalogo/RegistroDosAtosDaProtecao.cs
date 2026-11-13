namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O registro permanente do aceite da recomendação (ADR-0153): a linha do ato, com o retrato do item, que entra na mesma
/// transação da mudança do item e é descartada com ela se a gravação falhar. A solicitação e a decisão registram o ato
/// pelo próprio serviço.
/// </summary>
internal static class RegistroDosAtosDaProtecao
{
    public static void Registrar(this IAtoDaProtecaoDoCatalogoRepository atos, TipoDocumento tipo, AtoDaProtecao ato, AtorDoAto ator, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(atos);

        atos.Adicionar(AtoDaProtecaoDoCatalogo.Registrar(new TipoSobAprovacao(tipo).Retrato(), ato, ator, em));
    }

    public static void Registrar(this IAtoDaProtecaoDoCatalogoRepository atos, FatoCandidato fato, AtoDaProtecao ato, AtorDoAto ator, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(atos);

        atos.Adicionar(AtoDaProtecaoDoCatalogo.Registrar(new FatoSobAprovacao(fato).Retrato(), ato, ator, em));
    }
}
