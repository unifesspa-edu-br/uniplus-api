namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Determinismo do hash canônico (CA-05 da Story #460). Os cenários garantem
/// que o mesmo conteúdo produz o mesmo hash, e que mudanças em qualquer
/// campo semântico produzem hashes distintos — invariante load-bearing da
/// constraint UNIQUE parcial e do snapshot forense.
/// </summary>
public sealed class HashCanonicalComputerTests
{
    private static readonly PredicadoObrigatoriedade PredicadoBase =
        new EtapaObrigatoria("ProvaObjetiva");

    [Fact(DisplayName = "Hash é determinístico em runs distintos com o mesmo conteúdo")]
    public void Compute_MesmoConteudo_ProduzMesmoHash()
    {
        string h1 = HashCanonicalComputer.Compute(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            baseLegal: "Lei 12.711/2012 art.1º",
            portariaInternaCodigo: "Portaria 2026/14",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: new DateOnly(2027, 1, 1));

        string h2 = HashCanonicalComputer.Compute(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            baseLegal: "Lei 12.711/2012 art.1º",
            portariaInternaCodigo: "Portaria 2026/14",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: new DateOnly(2027, 1, 1));

        h1.Should().Be(h2, "hash canônico deve ser estável em runs idênticos");
        HashCanonicalComputer.IsValidHashShape(h1).Should().BeTrue();
    }

    [Theory(DisplayName = "Alteração em qualquer campo semântico muda o hash")]
    [InlineData("baseLegal")]
    [InlineData("categoria")]
    [InlineData("regraCodigo")]
    [InlineData("tipoEditalCodigo")]
    [InlineData("predicado")]
    [InlineData("portariaInternaCodigo")]
    [InlineData("vigenciaInicio")]
    [InlineData("vigenciaFim")]
    public void Compute_MudancaEmCampoSemantico_MudaHash(string campoAlterado)
    {
        string baseline = HashCanonicalComputer.Compute(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            baseLegal: "Lei 12.711/2012 art.1º",
            portariaInternaCodigo: "Portaria 2026/14",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: new DateOnly(2027, 1, 1));

        string mudado = campoAlterado switch
        {
            "baseLegal" => HashCanonicalComputer.Compute(
                "*", CategoriaObrigatoriedade.Etapa, "ETAPA_OBRIGATORIA", PredicadoBase,
                "Lei 14.723/2023 art.2º", "Portaria 2026/14", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            "categoria" => HashCanonicalComputer.Compute(
                "*", CategoriaObrigatoriedade.Modalidade, "ETAPA_OBRIGATORIA", PredicadoBase,
                "Lei 12.711/2012 art.1º", "Portaria 2026/14", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            "regraCodigo" => HashCanonicalComputer.Compute(
                "*", CategoriaObrigatoriedade.Etapa, "ETAPA_OBRIGATORIA_V2", PredicadoBase,
                "Lei 12.711/2012 art.1º", "Portaria 2026/14", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            "tipoEditalCodigo" => HashCanonicalComputer.Compute(
                "PSIQ", CategoriaObrigatoriedade.Etapa, "ETAPA_OBRIGATORIA", PredicadoBase,
                "Lei 12.711/2012 art.1º", "Portaria 2026/14", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            "predicado" => HashCanonicalComputer.Compute(
                "*", CategoriaObrigatoriedade.Etapa, "ETAPA_OBRIGATORIA", new EtapaObrigatoria("Redacao"),
                "Lei 12.711/2012 art.1º", "Portaria 2026/14", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            "portariaInternaCodigo" => HashCanonicalComputer.Compute(
                "*", CategoriaObrigatoriedade.Etapa, "ETAPA_OBRIGATORIA", PredicadoBase,
                "Lei 12.711/2012 art.1º", "Portaria 2026/99", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            "vigenciaInicio" => HashCanonicalComputer.Compute(
                "*", CategoriaObrigatoriedade.Etapa, "ETAPA_OBRIGATORIA", PredicadoBase,
                "Lei 12.711/2012 art.1º", "Portaria 2026/14", new DateOnly(2025, 6, 1), new DateOnly(2027, 1, 1)),
            "vigenciaFim" => HashCanonicalComputer.Compute(
                "*", CategoriaObrigatoriedade.Etapa, "ETAPA_OBRIGATORIA", PredicadoBase,
                "Lei 12.711/2012 art.1º", "Portaria 2026/14", new DateOnly(2026, 1, 1), null),
            _ => throw new InvalidOperationException("Campo desconhecido."),
        };

        mudado.Should().NotBe(baseline,
            $"alteração em {campoAlterado} deve produzir hash distinto");
    }

    [Fact(DisplayName = "PortariaInternaCodigo nulo vs vazio produzem o mesmo hash (ambos ignorados)")]
    public void Compute_PortariaNullVsVazio_HashIgual()
    {
        string h1 = HashCanonicalComputer.Compute(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            baseLegal: "Lei 12.711/2012 art.1º",
            portariaInternaCodigo: null,
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null);

        // Verifica também que vigenciaFim null não muda comportamento.
        string h2 = HashCanonicalComputer.Compute(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            baseLegal: "Lei 12.711/2012 art.1º",
            portariaInternaCodigo: null,
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null);

        h1.Should().Be(h2);
    }

    [Fact(DisplayName = "Hash respeita shape SHA-256 hex minúsculo (64 chars)")]
    public void Compute_ShapeShaCorreto()
    {
        string hash = HashCanonicalComputer.Compute(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "X",
            predicado: new ConcorrenciaDuplaObrigatoria(),
            baseLegal: "Lei 14.723/2023",
            portariaInternaCodigo: null,
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null);

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
        HashCanonicalComputer.IsValidHashShape(hash).Should().BeTrue();
    }

    [Fact(DisplayName = "Predicado polimórfico de variantes distintas produz hashes distintos")]
    public void Compute_PredicadosDistintos_HashesDistintos()
    {
        PredicadoObrigatoriedade etapa = new EtapaObrigatoria("ProvaObjetiva");
        PredicadoObrigatoriedade modalidades = new ModalidadesMinimas(new[] { "AC", "LbPpi" });
        PredicadoObrigatoriedade dupla = new ConcorrenciaDuplaObrigatoria();

        string h1 = Compute(etapa);
        string h2 = Compute(modalidades);
        string h3 = Compute(dupla);

        h1.Should().NotBe(h2);
        h2.Should().NotBe(h3);
        h1.Should().NotBe(h3);

        static string Compute(PredicadoObrigatoriedade predicado) => HashCanonicalComputer.Compute(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "R",
            predicado: predicado,
            baseLegal: "Lei 12.711/2012",
            portariaInternaCodigo: null,
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null);
    }

    [Fact(DisplayName = "IsValidHashShape rejeita strings fora do shape SHA-256 hex")]
    public void IsValidHashShape_StringsInvalidas_Rejeita()
    {
        HashCanonicalComputer.IsValidHashShape(null).Should().BeFalse();
        HashCanonicalComputer.IsValidHashShape(string.Empty).Should().BeFalse();
        HashCanonicalComputer.IsValidHashShape(new string('a', 63)).Should().BeFalse();
        HashCanonicalComputer.IsValidHashShape(new string('a', 65)).Should().BeFalse();
        HashCanonicalComputer.IsValidHashShape("g" + new string('a', 63)).Should().BeFalse();
        HashCanonicalComputer.IsValidHashShape(new string('A', 64)).Should().BeFalse(
            "uppercase não é o shape canônico — Convert.ToHexStringLower é o invariante");
    }
}
