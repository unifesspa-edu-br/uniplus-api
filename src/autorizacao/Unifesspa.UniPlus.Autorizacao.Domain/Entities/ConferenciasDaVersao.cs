namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As conferências de uma versão contra a que está em vigor e contra o catálogo. Valem na criação, para recusar cedo, e
/// de novo em cada aprovação, porque a concessão, o nome e o catálogo podem ter mudado enquanto a versão esperava.
/// </summary>
internal static class ConferenciasDaVersao
{
    /// <summary>Alcance diferente do da vigente. Sem versão vigente não há com que comparar: é uma nova primeira versão.</summary>
    public static DomainError? DaCriacao(VersaoDoPerfil versao, VersaoDoPerfil? vigente) => AlcanceDaVigente(versao, vigente);

    /// <summary>
    /// As conferências de cada aprovação: o documento do próprio perfil, o conteúdo pelo catálogo atual, o alcance
    /// gravado e a disponibilidade do nome.
    /// </summary>
    public static DomainError? DaAprovacao(
        VersaoDoPerfil versao,
        VersaoDoPerfil? vigente,
        ContextoDaVersao contexto,
        ICatalogoDePermissoesConsultavel catalogo) =>
        (versao.Documento is { Situacao: SituacaoDoAnexo.Verificado } ? null : PerfilErros.DocumentoNaoVerificado())
        ?? ConteudoNoCatalogo(versao, catalogo)
        ?? AlcanceGravado(versao, catalogo)
        ?? AlcanceDaVigente(versao, vigente)
        ?? (contexto.NomeEmUsoPorOutroPerfil ? PerfilErros.NomeDuplicado() : null);

    private static DomainError? ConteudoNoCatalogo(VersaoDoPerfil versao, ICatalogoDePermissoesConsultavel catalogo)
    {
        IReadOnlyList<string> codigos = versao.Codigos();

        if (codigos.Any(c => !catalogo.Existe(c)))
        {
            return PerfilErros.PermissaoInexistente();
        }

        return codigos.Any(catalogo.ReservadaAGovernanca) ? PerfilErros.PermissaoReservada() : null;
    }

    // O alcance gravado é o da criação: se o catálogo mudou o das permissões desde então, a versão já não é a mesma.
    private static DomainError? AlcanceGravado(VersaoDoPerfil versao, ICatalogoDePermissoesConsultavel catalogo) =>
        AlcanceDasPermissoes.De(versao.Codigos(), catalogo) == versao.Alcance ? null : PerfilErros.AlcanceAlterado();

    private static DomainError? AlcanceDaVigente(VersaoDoPerfil versao, VersaoDoPerfil? vigente) =>
        vigente is null || vigente.Alcance == versao.Alcance ? null : PerfilErros.AlcanceAlterado();
}
