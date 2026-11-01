namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O registro permanente dos atos do circuito de aprovação da proteção de dados (ADR-0153): a linha do ato, que entra na mesma transação da mudança do item e é descartada com ela se a gravação falhar.
/// </summary>
internal static class RegistroDosAtosDaProtecao
{
    public static void Registrar(
        this IAtoDaProtecaoDoCatalogoRepository atos,
        TipoDocumento tipo,
        AtoDaProtecao ato,
        AtorDoAto ator,
        DateTimeOffset em,
        ClassificacaoDeProtecao? recomendada = null,
        string? justificativa = null)
    {
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(tipo);

        atos.Adicionar(AtoDaProtecaoDoCatalogo.Registrar(
            CadastroDoCatalogo.TipoDocumento, tipo.Id, tipo.Codigo.Valor, tipo.Nome, ato, tipo.Protecao, recomendada, justificativa, ator, em));
    }

    public static void Registrar(
        this IAtoDaProtecaoDoCatalogoRepository atos,
        FatoCandidato fato,
        AtoDaProtecao ato,
        AtorDoAto ator,
        DateTimeOffset em,
        ClassificacaoDeProtecao? recomendada = null,
        string? justificativa = null)
    {
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(fato);

        atos.Adicionar(AtoDaProtecaoDoCatalogo.Registrar(
            CadastroDoCatalogo.Fato, fato.Id, fato.Codigo, fato.Nome, ato, fato.Protecao, recomendada, justificativa, ator, em));
    }
}
