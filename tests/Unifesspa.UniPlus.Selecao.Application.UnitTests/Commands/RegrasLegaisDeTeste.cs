namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As obrigatoriedades legais vigentes que os testes do gate de conformidade legal cadastram.</summary>
internal static class RegrasLegaisDeTeste
{
    public static IObrigatoriedadeLegalRepository RepositorioCom(params ObrigatoriedadeLegal[] regras)
    {
        IObrigatoriedadeLegalRepository repositorio = Substitute.For<IObrigatoriedadeLegalRepository>();
        repositorio.ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(regras);
        return repositorio;
    }

    public static ObrigatoriedadeLegal NovaRegra(string regraCodigo, PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: predicado,
            descricaoHumana: "Regra de teste do gate",
            baseLegal: "Lei de teste",
            vigenciaInicio: new DateOnly(2020, 1, 1)).Value!;

    // PROVA_OBJETIVA é o código do tipo congelado na única etapa do processo conforme: a avaliação compara
    // pelo código do tipo, nunca pelo rótulo editorial da etapa. Exigir OUTRO código reprova; exigir o MESMO
    // aprova. Basta para testar o fio do gate sem repetir as variantes que o domínio já cobre.
    public static ObrigatoriedadeLegal RegraQueReprova() =>
        NovaRegra("GATE-REPROVA", new EtapaObrigatoria("ETAPA_NAO_OFERTADA"));

    public static ObrigatoriedadeLegal RegraQueAprova() =>
        NovaRegra("GATE-APROVA", new EtapaObrigatoria("PROVA_OBJETIVA"));

    /// <summary>
    /// Regra com predicado que a factory recusa hoje, no estado em que o EF a reidrataria
    /// a partir de uma linha gravada antes de a validação de forma existir.
    /// </summary>
    /// <remarks>
    /// A propriedade é escrita por reflexão porque não há caminho legítimo até este estado:
    /// a única factory da entidade recusa a forma, e é justamente isso que torna a linha
    /// antiga invisível para quem só monta a regra pelo domínio. O EF chega lá todo dia, pelo
    /// setter privado, ao materializar o que está no banco.
    /// </remarks>
    public static ObrigatoriedadeLegal RegraGravadaSemPassarPelaFactory(
        string regraCodigo, PredicadoObrigatoriedade predicadoInvalido)
    {
        ObrigatoriedadeLegal regra = NovaRegra(regraCodigo, new EtapaObrigatoria("PROVA_OBJETIVA"));
        typeof(ObrigatoriedadeLegal)
            .GetProperty(nameof(ObrigatoriedadeLegal.Predicado))!
            .SetValue(regra, predicadoInvalido);
        return regra;
    }
}
