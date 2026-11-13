namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json.Serialization;

using Domain.Enums;
using Domain.ValueObjects;

/// <summary>
/// A regra de deliberação que o edital declara para uma banca: composição e, na votação do colegiado,
/// quórum, critério, desempate e encerramento. Os campos que o modo não admite ficam ausentes.
/// </summary>
/// <param name="Modo">Votação do colegiado ou ato individual.</param>
/// <param name="Integrantes">Titulares da banca, no mínimo 1.</param>
/// <param name="Suplentes">Suplentes da banca, no mínimo 0.</param>
/// <param name="Quorum">De 1 ao número de integrantes; só na votação do colegiado.</param>
/// <param name="Criterio">Maioria simples, maioria absoluta ou unanimidade; só na votação do colegiado.</param>
/// <param name="Desempate">Voto da presidência ou nova votação; obrigatório nos critérios de maioria com mais de 1 integrante.</param>
/// <param name="Encerramento">Todos os titulares ou a presidência ao atingir o quórum; só na votação do colegiado.</param>
/// <param name="SemUnanimidade">Favorável, desfavorável ou nova votação; obrigatório na unanimidade com mais de 1 integrante.</param>
public sealed record RegraDeDeliberacaoInput(
    [property: JsonRequired] ModoDeDeliberacao Modo,
    [property: JsonRequired] int Integrantes,
    [property: JsonRequired] int Suplentes,
    int? Quorum = null,
    CriterioDeVotacao? Criterio = null,
    DesempateDaVotacao? Desempate = null,
    EncerramentoDaVotacao? Encerramento = null,
    ResultadoSemUnanimidade? SemUnanimidade = null)
{
    internal DeclaracaoDaDeliberacao ParaDeclaracao() =>
        new(Modo, Integrantes, Suplentes, Quorum, Criterio, Desempate, Encerramento, SemUnanimidade);
}
