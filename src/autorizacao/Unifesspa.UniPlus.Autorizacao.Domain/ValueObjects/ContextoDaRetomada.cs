namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>A decisão de uma etapa da retomada, com o que o agregado precisa para decidi-la.</summary>
/// <param name="Decisor">Quem decide a etapa.</param>
/// <param name="DecisorTemConcessao">O decisor tem concessão do perfil, lida sob a trava do perfil.</param>
/// <param name="Etapa">A etapa que o decisor decide.</param>
/// <param name="Motivo">O texto informado; o agregado o normaliza.</param>
/// <param name="Catalogo">O catálogo atual, que diz se o perfil é protegido.</param>
/// <param name="Instante">O instante da decisão, pelo relógio injetado.</param>
public sealed record ContextoDaRetomada(
    Identidade Decisor,
    bool DecisorTemConcessao,
    EtapaDaVersao Etapa,
    string? Motivo,
    ICatalogoDePermissoesConsultavel Catalogo,
    DateTimeOffset Instante);

/// <summary>O que o perfil era no instante de um evento da contenção: a versão em vigor e se era protegido.</summary>
/// <param name="VersaoVigenteId">A versão em vigor; nula quando o perfil ainda não tem nenhuma.</param>
/// <param name="Protegido">A composição em vigor tem permissão pessoal ou acima, pelo catálogo atual.</param>
public sealed record RetratoDoPerfil(Guid? VersaoVigenteId, bool Protegido);
