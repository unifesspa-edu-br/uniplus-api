namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ManifestacoesDeTeste;

/// <summary>
/// Manifestações do mesmo item no mesmo instante saem das mais antigas para as mais recentes pela ordem de registro; a
/// ainda não gravada é a mais recente do seu instante.
/// </summary>
public sealed class ManifestacoesNoMesmoInstanteTests
{
    private static readonly DateTimeOffset Instante = TipoDocumentoNaAprovacaoBuilder.Segunda.AddHours(3);

    [Fact(DisplayName = "A manifestação ainda não gravada vem depois da gravada do mesmo instante")]
    public void Vigentes_NaoGravadaEAMaisRecente()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        ManifestacaoDaProtecaoDoCatalogo gravada = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Kevin, Instante).Value!;
        Numerar(gravada, 5);
        ManifestacoesDoCiclo manifestacoes = Do(tipo, gravada);

        ManifestacaoDaProtecaoDoCatalogo nova = manifestacoes.Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Lucia, Instante).Value!;

        manifestacoes.Vigentes.Should().Equal(gravada, nova);
    }

    [Theory(DisplayName = "Entre manifestações gravadas no mesmo instante vale a ordem de registro, qualquer que seja a posição na lista")]
    [InlineData(9, 3)]
    [InlineData(3, 9)]
    public void Vigentes_PelaOrdemDeRegistro(long ordemDeKevin, long ordemDeLucia)
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        ManifestacaoDaProtecaoDoCatalogo kevin = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Kevin, Instante).Value!;
        ManifestacaoDaProtecaoDoCatalogo lucia = Do(tipo, kevin).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Lucia, Instante).Value!;
        Numerar(kevin, ordemDeKevin);
        Numerar(lucia, ordemDeLucia);

        IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> vigentes = Do(tipo, kevin, lucia).Vigentes;

        vigentes.Select(static m => m.Autor.Nome).Should().Equal(ordemDeKevin < ordemDeLucia ? [Kevin.Nome, Lucia.Nome] : [Lucia.Nome, Kevin.Nome]);
    }

    // A ordem de registro é do banco: o teste simula a manifestação relida com o número que o banco lhe deu.
    private static void Numerar(ManifestacaoDaProtecaoDoCatalogo manifestacao, long ordem) =>
        typeof(ManifestacaoDaProtecaoDoCatalogo).GetProperty(nameof(ManifestacaoDaProtecaoDoCatalogo.OrdemDeRegistro), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(manifestacao, ordem);
}
